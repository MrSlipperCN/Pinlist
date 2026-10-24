using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Interop;
using System.Windows.Threading;
using Pinlist.Core;
using Pinlist.Storage.SQLite;
using Forms = System.Windows.Forms;

namespace Pinlist.Desktop;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x504C;
    private const int WmHotkey = 0x0312;
    private const int WmNcHitTest = 0x0084;
    private const int WmSysCommand = 0x0112;
    private const int ScMinimize = 0xF020;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int GwlHwndParent = -8;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const long WsExTopmost = 0x00000008L;
    private static readonly nint HwndTop = nint.Zero;
    private static readonly nint HwndTopmost = new(-1);
    private static readonly nint HwndNoTopmost = new(-2);
    private const double IdleOpacity = 0.72;
    private const double HoverOpacity = 0.98;
    private const uint ModAlt = 0x0001;
    private const uint ModShift = 0x0004;
    private const uint VirtualKeyP = 0x50;
    private readonly ObservableCollection<PinTask> _tasks = [];
    private readonly IPinTaskRepository _repository;
    private readonly AppSettingsStore _settingsStore;
    private readonly DispatcherTimer _fullscreenTimer;
    private readonly DispatcherTimer _taskSyncTimer;
    private readonly DispatcherTimer _undoTimer;
    private readonly DispatcherTimer _desktopHostTimer;
    private readonly Forms.NotifyIcon _trayIcon;
    private readonly Forms.ToolStripMenuItem _startWithWindowsMenuItem;
    private readonly Forms.ToolStripMenuItem _hotkeyMenuItem;
    private readonly Forms.ToolStripMenuItem _hideDuringFullscreenMenuItem;
    private readonly Forms.ToolStripMenuItem _openMenuItem;
    private readonly Forms.ToolStripMenuItem _quickAddMenuItem;
    private readonly Forms.ToolStripMenuItem _historyMenuItem;
    private readonly Forms.ToolStripMenuItem _languageMenuItem;
    private readonly Forms.ToolStripMenuItem _englishMenuItem;
    private readonly Forms.ToolStripMenuItem _chineseMenuItem;
    private readonly Forms.ToolStripMenuItem _exitMenuItem;
    private PinTask? _selectedTask;
    private AppSettings _settings;
    private nint _windowHandle;
    private double _expandedHeight = 620;
    private bool _isCollapsed;
    private bool _isHiddenForFullscreen;
    private bool _isExitRequested;
    private bool _hotkeyRegistered;
    private bool _isDialogOpen;
    private bool _isPointerOver;
    private uint _taskbarCreatedMessage;
    private string? _taskSnapshot;
    private System.Windows.Point _dragStartPoint;
    private PinTask? _draggedTask;
    private ListBoxItem? _dropTargetItem;
    private bool _dropAfterTarget;
    private Func<Task>? _undoAction;

    public MainWindow()
    {
        InitializeComponent();
        var dataDirectory = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Pinlist");
        _repository = new SqlitePinTaskRepository(System.IO.Path.Combine(dataDirectory, "pinlist.db"));
        _settingsStore = new AppSettingsStore(System.IO.Path.Combine(dataDirectory, "settings.json"));
        _settings = _settingsStore.Load();
        UiText.Apply(_settings.Language ?? UiText.DetectLanguage());
        _fullscreenTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _fullscreenTimer.Tick += FullscreenTimer_Tick;
        _taskSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _taskSyncTimer.Tick += TaskSyncTimer_Tick;
        _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _undoTimer.Tick += (_, _) => HideUndo();
        _desktopHostTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _desktopHostTimer.Tick += (_, _) =>
        {
            EnsureDesktopOwnership();
            EnsureTopmostState();
            KeepVisibleWhenDesktopIsForeground();
        };

        _startWithWindowsMenuItem = new Forms.ToolStripMenuItem(UiText.Get("StartWithWindows"))
        {
            CheckOnClick = true,
            Checked = StartupManager.IsEnabled()
        };
        _startWithWindowsMenuItem.CheckedChanged += StartWithWindowsMenuItem_CheckedChanged;

        _hotkeyMenuItem = new Forms.ToolStripMenuItem(UiText.Get("GlobalShortcut"))
        {
            CheckOnClick = true,
            Checked = _settings.GlobalHotkeyEnabled
        };
        _hotkeyMenuItem.CheckedChanged += HotkeyMenuItem_CheckedChanged;

        _hideDuringFullscreenMenuItem = new Forms.ToolStripMenuItem(UiText.Get("HideDuringFullscreen"))
        {
            CheckOnClick = true,
            Checked = _settings.HideDuringFullscreen
        };
        _hideDuringFullscreenMenuItem.CheckedChanged += HideDuringFullscreenMenuItem_CheckedChanged;

        _openMenuItem = new Forms.ToolStripMenuItem(UiText.Get("OpenPinlist"), null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        _quickAddMenuItem = new Forms.ToolStripMenuItem(UiText.Get("QuickAdd"), null, (_, _) => Dispatcher.Invoke(ShowQuickAdd));
        _historyMenuItem = new Forms.ToolStripMenuItem(UiText.Get("HistoryTitle"), null, (_, _) => Dispatcher.Invoke(ShowHistory));
        _languageMenuItem = new Forms.ToolStripMenuItem(UiText.Get("Language"));
        _englishMenuItem = new Forms.ToolStripMenuItem("English") { CheckOnClick = false };
        _chineseMenuItem = new Forms.ToolStripMenuItem("简体中文") { CheckOnClick = false };
        _englishMenuItem.Click += (_, _) => Dispatcher.Invoke(() => ChangeLanguage(UiText.English));
        _chineseMenuItem.Click += (_, _) => Dispatcher.Invoke(() => ChangeLanguage(UiText.SimplifiedChinese));
        _languageMenuItem.DropDownItems.Add(_englishMenuItem);
        _languageMenuItem.DropDownItems.Add(_chineseMenuItem);
        _exitMenuItem = new Forms.ToolStripMenuItem(UiText.Get("Exit"), null, (_, _) => Dispatcher.Invoke(ExitApplication));

        var trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add(_openMenuItem);
        trayMenu.Items.Add(_quickAddMenuItem);
        trayMenu.Items.Add(_historyMenuItem);
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add(_startWithWindowsMenuItem);
        trayMenu.Items.Add(_hotkeyMenuItem);
        trayMenu.Items.Add(_hideDuringFullscreenMenuItem);
        trayMenu.Items.Add(_languageMenuItem);
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add(_exitMenuItem);

        _trayIcon = new Forms.NotifyIcon
        {
            Text = "Pinlist",
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)
                   ?? System.Drawing.SystemIcons.Application,
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);

        TaskList.ItemsSource = _tasks;
        ApplyLocalizedText();
    }

    private void EnsureLanguageChosen()
    {
        if (!string.IsNullOrWhiteSpace(_settings.Language)) return;

        var selector = new LanguageSelectionWindow
        {
            Owner = this,
            Topmost = true
        };
        ChangeLanguage(selector.ShowDialog() == true
            ? selector.SelectedLanguage
            : UiText.DetectLanguage());
    }

    private void ChangeLanguage(string language)
    {
        var normalized = UiText.Normalize(language);
        UiText.Apply(normalized);
        _settings = _settings with { Language = normalized };
        ApplyLocalizedText();
        TrySaveSettings();
    }

    private void ApplyLocalizedText()
    {
        _openMenuItem.Text = UiText.Get("OpenPinlist");
        _quickAddMenuItem.Text = UiText.Get("QuickAdd");
        _historyMenuItem.Text = UiText.Get("HistoryTitle");
        _startWithWindowsMenuItem.Text = UiText.Get("StartWithWindows");
        _hotkeyMenuItem.Text = UiText.Get("GlobalShortcut");
        _hideDuringFullscreenMenuItem.Text = UiText.Get("HideDuringFullscreen");
        _languageMenuItem.Text = UiText.Get("Language");
        _exitMenuItem.Text = UiText.Get("Exit");
        _englishMenuItem.Checked = UiText.CurrentLanguage == UiText.English;
        _chineseMenuItem.Checked = UiText.CurrentLanguage == UiText.SimplifiedChinese;
        CollapseButton.ToolTip = UiText.Get(_isCollapsed ? "Expand" : "Collapse");
        AlwaysOnTopButton.ToolTip = UiText.Get(_settings.AlwaysOnTop ? "AlwaysOnTopOn" : "AlwaysOnTopOff");
        OpenCountText.Text = UiText.OpenCount(_tasks.Count(task => task.Status == PinTaskStatus.Open));
        TaskList.Items.Refresh();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        RestoreWindowPlacement();
        SetCollapsed(true);
        ApplyAlwaysOnTop(_settings.AlwaysOnTop);
        UpdateWindowOpacity(animate: false);
        EnsureLanguageChosen();
        _fullscreenTimer.Start();
        _taskSyncTimer.Start();
        _desktopHostTimer.Start();
        try
        {
            await ReloadTasksAsync();
        }
        catch (Exception exception)
        {
            ShowOperationError(UiText.Get("CouldNotLoadTasks"), exception);
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _windowHandle = new WindowInteropHelper(this).Handle;
        RemoveMinimizeAndMaximizeStyles();
        ApplyDesktopOwnership();
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        HwndSource.FromHwnd(_windowHandle)?.AddHook(WindowMessageHook);
        UpdateHotkeyRegistration(_settings.GlobalHotkeyEnabled);
    }

    private void ApplyDesktopOwnership()
    {
        if (_windowHandle == nint.Zero) return;
        var owner = GetShellWindow();
        if (owner == nint.Zero) return;
        _ = SetWindowLongPtr(_windowHandle, GwlHwndParent, owner);
        _ = SetWindowPos(_windowHandle, Topmost ? HwndTopmost : HwndTop,
            0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private void EnsureDesktopOwnership()
    {
        if (_windowHandle == nint.Zero) return;
        var shellWindow = GetShellWindow();
        if (shellWindow != nint.Zero
            && GetWindowLongPtr(_windowHandle, GwlHwndParent) != shellWindow)
        {
            ApplyDesktopOwnership();
        }
    }

    private void EnsureTopmostState()
    {
        if (_windowHandle == nint.Zero
            || !IsVisible
            || !IsEnabled
            || WindowState != WindowState.Normal)
        {
            return;
        }

        var isTopmost = (GetWindowLongPtr(_windowHandle, GwlExStyle).ToInt64() & WsExTopmost) != 0;

        // HWND_TOPMOST is a band, not a single absolute layer. Reasserting the
        // position keeps Pinlist at the front of that band even when another
        // topmost application is shown later. SWP_NOACTIVATE prevents focus
        // stealing, and IsEnabled keeps owned modal dialogs above this window.
        if (_settings.AlwaysOnTop)
        {
            _ = SetWindowPos(_windowHandle, HwndTopmost,
                0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
            return;
        }

        if (!isTopmost) return;

        _ = SetWindowPos(_windowHandle, HwndNoTopmost,
            0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private void RemoveMinimizeAndMaximizeStyles()
    {
        if (_windowHandle == nint.Zero) return;
        var style = GetWindowLongPtr(_windowHandle, GwlStyle).ToInt64();
        style &= ~(WsMinimizeBox | WsMaximizeBox);
        _ = SetWindowLongPtr(_windowHandle, GwlStyle, new nint(style));
    }

    private void KeepVisibleWhenDesktopIsForeground()
    {
        if (_windowHandle == nint.Zero
            || !IsVisible
            || WindowState != WindowState.Normal
            || !FullscreenDetector.IsDesktopForegroundWindow())
        {
            return;
        }

        if (_isHiddenForFullscreen)
        {
            _isHiddenForFullscreen = false;
            IsHitTestVisible = true;
            UpdateWindowOpacity(animate: false);
        }

        // Win+D raises the desktop above ordinary windows. Reinsert Pinlist at
        // the top of the ordinary (non-topmost) band without activating it.
        // Other applications still cover Pinlist as soon as they are focused.
        if (!_settings.AlwaysOnTop)
        {
            _ = SetWindowPos(_windowHandle, HwndTop,
                0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }
    }

    private void RestoreWindowPlacement()
    {
        Width = Math.Clamp(_settings.Width, MinWidth, 1600);
        _expandedHeight = Math.Clamp(_settings.Height, MinHeight, 1400);
        Height = _expandedHeight;

        if (IsVisiblePlacement(_settings.Left, _settings.Top, Width, _expandedHeight))
        {
            Left = _settings.Left;
            Top = _settings.Top;
            return;
        }

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 18;
        Top = workArea.Top + 18;
    }

    private static bool IsVisiblePlacement(double left, double top, double width, double height)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top)) return false;

        var right = left + width;
        var bottom = top + height;
        return right > SystemParameters.VirtualScreenLeft + 40
               && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 40
               && bottom > SystemParameters.VirtualScreenTop + 40
               && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 40;
    }

    private async Task ReloadTasksAsync(Guid? selectId = null)
    {
        var openTasks = await _repository.ListAsync(PinTaskStatus.Open);
        var closedToday = (await _repository.ListAsync(PinTaskStatus.Closed))
            .Where(task => task.ClosedAt?.ToLocalTime().Date == DateTime.Today);
        var tasks = openTasks.Concat(closedToday).OrderBy(task => task.SortOrder).ToArray();
        var snapshot = string.Join('|', tasks.Select(task => $"{task.Id:N}:{task.Status}:{task.UpdatedAt.UtcTicks}:{task.SortOrder}"));
        if (_taskSnapshot is not null && snapshot == _taskSnapshot) return;

        _taskSnapshot = snapshot;
        _tasks.Clear();
        foreach (var task in tasks) _tasks.Add(task);

        OpenCountText.Text = UiText.OpenCount(openTasks.Count);
        EmptyStatePanel.Visibility = _tasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (selectId is { } id)
            TaskList.SelectedItem = _tasks.FirstOrDefault(task => task.Id == id);
    }

    private async void TaskSyncTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsVisible || _isDialogOpen) return;
        try
        {
            await ReloadTasksAsync(_selectedTask?.Id);
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // A concurrent MCP write can briefly hold the database; the next tick retries.
        }
        catch (Exception exception)
        {
            _taskSyncTimer.Stop();
            ShowOperationError(UiText.Get("BackgroundSyncStopped"), exception);
        }
    }

    private async Task ShowTaskDialogAsync(PinTask? task = null)
    {
        if (_isDialogOpen) return;
        _isDialogOpen = true;
        try
        {
            var editor = new TaskEditorWindow(task) { Topmost = true };
            if (editor.ShowDialog() != true) return;

            var isNew = task is null;
            task ??= new PinTask { Title = editor.TaskTitle };
            if (editor.WantsClose)
                task.Close();
            else
                task.Update(editor.TaskTitle, editor.TaskContext, editor.Urgency, editor.IsPinned);

            if (isNew) await _repository.SaveNewAtTopAsync(task);
            else await _repository.SaveAsync(task);
            _selectedTask = null;
            TaskList.SelectedItem = null;
            await ReloadTasksAsync();
        }
        catch (Exception exception)
        {
            ShowOperationError(UiText.Get("CouldNotSaveTask"), exception);
        }
        finally
        {
            _selectedTask = null;
            TaskList.SelectedItem = null;
            _isDialogOpen = false;
        }
    }

    private async void TaskList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) is not null) return;
        _selectedTask = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as PinTask;
        if (_selectedTask is null) return;
        await ShowTaskDialogAsync(_selectedTask);
    }

    private async void EditTaskCommand_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.OriginalSource is not MenuItem menuItem
            || ItemsControl.ItemsControlFromItemContainer(menuItem) is not ContextMenu { PlacementTarget: ListBoxItem item }) return;
        _selectedTask = item.DataContext as PinTask;
        if (_selectedTask is null) return;
        await ShowTaskDialogAsync(_selectedTask);
    }

    private async void CompleteTask_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not System.Windows.Controls.Button { Tag: PinTask task }) return;
        try
        {
            var originalStatus = task.Status;
            if (task.Status == PinTaskStatus.Closed) task.Restore(); else task.Close();
            await _repository.SaveAsync(task);
            await ReloadTasksAsync();
            ShowUndo(UiText.Get(originalStatus == PinTaskStatus.Closed ? "TaskRestored" : "TaskCompleted"), async () =>
            {
                if (originalStatus == PinTaskStatus.Closed) task.Close(); else task.Restore();
                await _repository.SaveAsync(task);
            });
        }
        catch (Exception exception)
        {
            ShowOperationError(UiText.Get("CouldNotUpdateTask"), exception);
        }
    }

    private async void ImportantTask_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not System.Windows.Controls.Button { Tag: PinTask task }) return;
        try
        {
            var wasImportant = task.IsPinned;
            task.SetImportant(!task.IsPinned);
            await _repository.SaveAsync(task);
            await ReloadTasksAsync();
            ShowUndo(UiText.Get(task.IsPinned ? "MarkedImportant" : "ImportanceRemoved"), async () =>
            {
                task.SetImportant(wasImportant);
                await _repository.SaveAsync(task);
            });
        }
        catch (Exception exception)
        {
            ShowOperationError(UiText.Get("CouldNotUpdateImportance"), exception);
        }
    }

    private void TaskList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(TaskList);
        _draggedTask = FindAncestor<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) is null
            ? FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as PinTask
            : null;
    }

    private void TaskList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _draggedTask is null) return;
        var position = e.GetPosition(TaskList);
        if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        System.Windows.DragDrop.DoDragDrop(TaskList, _draggedTask, System.Windows.DragDropEffects.Move);
        _draggedTask = null;
    }

    private async void TaskList_Drop(object sender, System.Windows.DragEventArgs e)
    {
        var dropAfterTarget = _dropAfterTarget;
        ClearDropIndicator();
        if (!e.Data.GetDataPresent(typeof(PinTask))) return;
        var source = e.Data.GetData(typeof(PinTask)) as PinTask;
        var target = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject)?.DataContext as PinTask;
        if (source is null || target is null || source == target) return;

        var oldIndex = _tasks.IndexOf(source);
        var targetIndex = _tasks.IndexOf(target);
        if (oldIndex < 0 || targetIndex < 0) return;
        var insertionIndex = targetIndex + (dropAfterTarget ? 1 : 0);
        if (oldIndex < insertionIndex) insertionIndex--;
        var newIndex = Math.Clamp(insertionIndex, 0, _tasks.Count - 1);
        if (newIndex == oldIndex) return;
        var previousOrder = _tasks.ToArray();
        try
        {
            _tasks.Move(oldIndex, newIndex);
            await _repository.SaveOrderAsync(_tasks);
            _taskSnapshot = null;
            await ReloadTasksAsync();
            ShowUndo(UiText.Get("OrderUpdated"), () => _repository.SaveOrderAsync(previousOrder));
        }
        catch (Exception exception)
        {
            ShowOperationError(UiText.Get("CouldNotSaveOrder"), exception);
            _taskSnapshot = null;
            await ReloadTasksAsync();
        }
    }

    private void TaskList_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        AutoScrollTaskList(e.GetPosition(TaskList).Y);
        var target = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (target is not null)
        {
            var pointer = e.GetPosition(target);
            SetDropIndicator(target, pointer.Y >= target.ActualHeight / 2);
        }
        else
        {
            ClearDropIndicator();
        }
        e.Effects = target is null ? System.Windows.DragDropEffects.None : System.Windows.DragDropEffects.Move;
        e.Handled = true;
    }

    private void TaskList_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (!TaskList.IsMouseOver) ClearDropIndicator();
    }

    private void AutoScrollTaskList(double pointerY)
    {
        const double edge = 32;
        var scrollViewer = FindVisualChild<ScrollViewer>(TaskList);
        if (scrollViewer is null) return;

        if (pointerY < edge) scrollViewer.LineUp();
        else if (pointerY > TaskList.ActualHeight - edge) scrollViewer.LineDown();
    }

    private void SetDropIndicator(ListBoxItem target, bool afterTarget)
    {
        if (_dropTargetItem == target && _dropAfterTarget == afterTarget) return;
        ClearDropIndicator();
        _dropTargetItem = target;
        _dropAfterTarget = afterTarget;
        target.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(124, 145, 255));
        target.BorderThickness = afterTarget ? new Thickness(0, 0, 0, 2) : new Thickness(0, 2, 0, 0);
    }

    private void ClearDropIndicator()
    {
        if (_dropTargetItem is not null)
        {
            _dropTargetItem.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
            _dropTargetItem.ClearValue(System.Windows.Controls.Control.BorderThicknessProperty);
        }
        _dropTargetItem = null;
        _dropAfterTarget = false;
    }

    private void ShowOperationError(string message, Exception exception)
    {
        AppLog.Write($"{message}: {exception}");
        _trayIcon.BalloonTipTitle = "Pinlist";
        _trayIcon.BalloonTipText = $"{message}. {exception.Message}";
        _trayIcon.ShowBalloonTip(4000);
    }

    private void ShowUndo(string message, Func<Task> undoAction)
    {
        _undoAction = undoAction;
        UndoText.Text = message;
        UndoBar.Visibility = Visibility.Visible;
        _undoTimer.Stop();
        _undoTimer.Start();
    }

    private void HideUndo()
    {
        _undoTimer.Stop();
        _undoAction = null;
        UndoBar.Visibility = Visibility.Collapsed;
    }

    private async void Undo_Click(object sender, RoutedEventArgs e)
    {
        var undoAction = _undoAction;
        HideUndo();
        if (undoAction is null) return;
        try
        {
            await undoAction();
            _taskSnapshot = null;
            await ReloadTasksAsync();
        }
        catch (Exception exception)
        {
            ShowOperationError(UiText.Get("CouldNotUndo"), exception);
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Z || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || _undoAction is null) return;
        e.Handled = true;
        Undo_Click(sender, e);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject current) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(current); index++)
        {
            var child = VisualTreeHelper.GetChild(current, index);
            if (child is T match) return match;
            var descendant = FindVisualChild<T>(child);
            if (descendant is not null) return descendant;
        }
        return null;
    }

    private async void BeginNewTask()
    {
        if (_isCollapsed) SetCollapsed(false);
        await ShowTaskDialogAsync();
    }

    private void AddTask_Click(object sender, RoutedEventArgs e) => BeginNewTask();

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        DragMove();
    }

    private void Collapse_Click(object sender, RoutedEventArgs e) => SetCollapsed(!_isCollapsed);

    private void SetCollapsed(bool collapsed)
    {
        if (collapsed == _isCollapsed && IsLoaded) return;

        if (collapsed)
        {
            if (Height > 100) _expandedHeight = Height;
            TaskList.Visibility = Visibility.Collapsed;
            MinHeight = 56;
            Height = 56;
            CollapseIcon.Data = Geometry.Parse("M3,6 L8,11 L13,6");
            CollapseButton.ToolTip = UiText.Get("Expand");
        }
        else
        {
            TaskList.Visibility = Visibility.Visible;
            MinHeight = 420;
            Height = Math.Max(_expandedHeight, MinHeight);
            CollapseIcon.Data = Geometry.Parse("M3,10 L8,5 L13,10");
            CollapseButton.ToolTip = UiText.Get("Collapse");
        }

        _isCollapsed = collapsed;
    }

    private void FullscreenTimer_Tick(object? sender, EventArgs e)
    {
        if (!_settings.HideDuringFullscreen || _windowHandle == nint.Zero) return;

        var foreground = FullscreenDetector.GetForegroundHandle();
        var shouldHide = FullscreenDetector.IsForegroundWindowFullscreen(_windowHandle)
                         && FullscreenDetector.IsOnSameMonitor(_windowHandle, foreground);
        if (shouldHide == _isHiddenForFullscreen) return;

        _isHiddenForFullscreen = shouldHide;
        IsHitTestVisible = !shouldHide;
        UpdateWindowOpacity(animate: true);
    }

    private nint WindowMessageHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (_taskbarCreatedMessage != 0 && message == _taskbarCreatedMessage)
        {
            Dispatcher.BeginInvoke(() =>
            {
                EnsureDesktopOwnership();
                EnsureTopmostState();
            }, DispatcherPriority.Background);
        }

        if (message == WmSysCommand
            && (wParam.ToInt64() & 0xFFF0) == ScMinimize)
        {
            handled = true;
            return nint.Zero;
        }

        if (message == WmNcHitTest && !_isCollapsed && WindowState == WindowState.Normal)
        {
            GetWindowRect(hwnd, out var rect);
            var packed = lParam.ToInt64();
            var x = unchecked((short)(packed & 0xFFFF));
            var y = unchecked((short)((packed >> 16) & 0xFFFF));
            const int edge = 7;
            var left = x <= rect.Left + edge;
            var right = x >= rect.Right - edge;
            var top = y <= rect.Top + edge;
            var bottom = y >= rect.Bottom - edge;
            var hit = top && left ? 13 : top && right ? 14 : bottom && left ? 16 : bottom && right ? 17
                : left ? 10 : right ? 11 : top ? 12 : bottom ? 15 : 0;
            if (hit != 0)
            {
                handled = true;
                return hit;
            }
        }

        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            ShowQuickAdd();
        }

        return nint.Zero;
    }

    private void UpdateHotkeyRegistration(bool enabled)
    {
        if (_windowHandle == nint.Zero) return;

        if (_hotkeyRegistered)
        {
            UnregisterHotKey(_windowHandle, HotkeyId);
            _hotkeyRegistered = false;
        }

        if (!enabled) return;

        _hotkeyRegistered = RegisterHotKey(_windowHandle, HotkeyId, ModAlt | ModShift, VirtualKeyP);
        if (!_hotkeyRegistered)
        {
            _hotkeyMenuItem.CheckedChanged -= HotkeyMenuItem_CheckedChanged;
            _hotkeyMenuItem.Checked = false;
            _hotkeyMenuItem.CheckedChanged += HotkeyMenuItem_CheckedChanged;
            _settings = _settings with { GlobalHotkeyEnabled = false };
        }
    }

    private void ShowFromTray()
    {
        Show();
        EnsureDesktopOwnership();
        EnsureTopmostState();
        IsHitTestVisible = true;
        WindowState = WindowState.Normal;
        UpdateWindowOpacity(animate: false);
        Activate();
    }

    internal void ShowFromExternalActivation() => ShowFromTray();

    private void ShowQuickAdd()
    {
        ShowFromTray();
        if (_isCollapsed) SetCollapsed(false);
        BeginNewTask();
    }

    private async void ShowHistory()
    {
        ShowFromTray();
        var history = new TaskHistoryWindow(_repository) { Topmost = true };
        history.ShowDialog();
        if (history.TasksChanged)
        {
            _taskSnapshot = null;
            await ReloadTasksAsync();
        }
    }

    private void HideToTray()
    {
        Hide();
        _trayIcon.Visible = true;
    }

    private void StartWithWindowsMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        try
        {
            StartupManager.SetEnabled(_startWithWindowsMenuItem.Checked);
            _settings = _settings with { StartWithWindows = _startWithWindowsMenuItem.Checked };
            SaveSettings();
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or SecurityException)
        {
            _startWithWindowsMenuItem.CheckedChanged -= StartWithWindowsMenuItem_CheckedChanged;
            _startWithWindowsMenuItem.Checked = StartupManager.IsEnabled();
            _startWithWindowsMenuItem.CheckedChanged += StartWithWindowsMenuItem_CheckedChanged;
        }
    }

    private void HotkeyMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        _settings = _settings with { GlobalHotkeyEnabled = _hotkeyMenuItem.Checked };
        UpdateHotkeyRegistration(_settings.GlobalHotkeyEnabled);
        SaveSettings();
    }

    private void HideDuringFullscreenMenuItem_CheckedChanged(object? sender, EventArgs e)
    {
        _settings = _settings with { HideDuringFullscreen = _hideDuringFullscreenMenuItem.Checked };
        if (!_settings.HideDuringFullscreen && _isHiddenForFullscreen)
        {
            _isHiddenForFullscreen = false;
            IsHitTestVisible = true;
            UpdateWindowOpacity(animate: true);
        }
        SaveSettings();
    }

    private void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
    {
        ApplyAlwaysOnTop(!_settings.AlwaysOnTop);
        _settings = _settings with { AlwaysOnTop = Topmost };
        SaveSettings();
    }

    private void ApplyAlwaysOnTop(bool enabled)
    {
        ApplyDesktopOwnership();
        if (enabled)
        {
            Topmost = true;
        }
        else
        {
            Topmost = false;
            ApplyDesktopOwnership();
        }
        AlwaysOnTopButton.ToolTip = UiText.Get(enabled ? "AlwaysOnTopOn" : "AlwaysOnTopOff");
        AlwaysOnTopButton.Foreground = enabled
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(143, 168, 255))
            : new SolidColorBrush(System.Windows.Media.Color.FromRgb(174, 179, 190));
        if (_windowHandle != nint.Zero && enabled)
        {
            SetWindowPos(_windowHandle, HwndTopmost,
                0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }
        else if (_windowHandle != nint.Zero)
        {
            SetWindowPos(_windowHandle, HwndNoTopmost,
                0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
        }
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isPointerOver = true;
        UpdateWindowOpacity(animate: true);
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isPointerOver = false;
        UpdateWindowOpacity(animate: true);
    }

    private void UpdateWindowOpacity(bool animate)
    {
        var target = _isHiddenForFullscreen ? 0 : _isPointerOver ? HoverOpacity : IdleOpacity;
        BeginAnimation(OpacityProperty, null);
        if (!animate)
        {
            Opacity = target;
            return;
        }

        BeginAnimation(OpacityProperty, new DoubleAnimation(target, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void SaveSettings()
    {
        var left = Left;
        var top = Top;
        var width = Width;
        var height = _isCollapsed ? _expandedHeight : Height;

        var settings = new AppSettings(
            left,
            top,
            width,
            height,
            _isCollapsed,
            _settings.HideDuringFullscreen,
            _startWithWindowsMenuItem.Checked,
            _settings.GlobalHotkeyEnabled,
            _settings.AlwaysOnTop,
            _settings.Language);
        _settingsStore.Save(settings);
        _settings = settings;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_isExitRequested)
        {
            e.Cancel = true;
            TrySaveSettings();
            HideToTray();
            return;
        }

        _fullscreenTimer.Stop();
        _taskSyncTimer.Stop();
        _undoTimer.Stop();
        _desktopHostTimer.Stop();
        if (_hotkeyRegistered) UnregisterHotKey(_windowHandle, HotkeyId);
        TrySaveSettings();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }

    private void TrySaveSettings()
    {
        try
        {
            SaveSettings();
        }
        catch (IOException)
        {
            // Closing must never be blocked by a transient settings write failure.
        }
        catch (UnauthorizedAccessException)
        {
            // The task database remains unaffected if preferences cannot be saved.
        }
    }

    private void ExitApplication()
    {
        _isExitRequested = true;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void Tray_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void Minimize_Click(object sender, RoutedEventArgs e) => HideToTray();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string message);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint hWnd, out WindowRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetWindowLongPtr(nint hWnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int index, nint value);

    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

}
