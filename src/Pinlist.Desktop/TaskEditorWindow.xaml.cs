using System.Windows;
using System.Windows.Input;
using Pinlist.Core;

namespace Pinlist.Desktop;

public partial class TaskEditorWindow : Window
{
    public string TaskTitle => TitleBox.Text;
    public string TaskContext => ContextBox.Text;
    public TaskUrgency Urgency => HighUrgency.IsChecked == true
        ? TaskUrgency.High
        : LowUrgency.IsChecked == true ? TaskUrgency.Low : TaskUrgency.Normal;
    public bool IsPinned => PinnedBox.IsChecked == true;
    public bool WantsClose { get; private set; }

    public TaskEditorWindow(PinTask? task = null)
    {
        InitializeComponent();
        SetUrgency(task?.Urgency ?? TaskUrgency.Normal);

        if (task is not null)
        {
            HeadingText.Text = "Edit task";
            SaveButton.Content = "Save changes";
            TitleBox.Text = task.Title;
            ContextBox.Text = task.Context;
            PinnedBox.IsChecked = task.IsPinned;
            DoneButton.Visibility = Visibility.Visible;
        }

        Loaded += (_, _) => TitleBox.Focus();
    }

    private void SetUrgency(TaskUrgency urgency)
    {
        LowUrgency.IsChecked = urgency == TaskUrgency.Low;
        NormalUrgency.IsChecked = urgency == TaskUrgency.Normal;
        HighUrgency.IsChecked = urgency == TaskUrgency.High;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleBox.Text))
        {
            StatusText.Text = "Give this task a title.";
            TitleBox.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        WantsClose = true;
        DialogResult = true;
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape) DialogResult = false;
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) Save_Click(sender, e);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
