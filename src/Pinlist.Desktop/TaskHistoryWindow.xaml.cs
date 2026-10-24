using System.Windows;
using System.Windows.Input;
using Pinlist.Core;

namespace Pinlist.Desktop;

public partial class TaskHistoryWindow : Window
{
    private readonly IPinTaskRepository _repository;
    public bool TasksChanged { get; private set; }

    public TaskHistoryWindow(IPinTaskRepository repository)
    {
        InitializeComponent();
        _repository = repository;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e) => await ReloadAsync();

    private async Task ReloadAsync()
    {
        try
        {
            var closed = await _repository.ListAsync(PinTaskStatus.Closed);
            var trashed = await _repository.ListAsync(PinTaskStatus.Trashed);
            var tasks = closed.Concat(trashed).OrderByDescending(task => task.UpdatedAt).ToArray();
            HistoryList.ItemsSource = tasks;
            EmptyText.Visibility = tasks.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = string.Empty;
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
        }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: PinTask task }) return;
        try
        {
            task.Restore();
            await _repository.SaveNewAtTopAsync(task);
            TasksChanged = true;
            await ReloadAsync();
        }
        catch (Exception exception)
        {
            StatusText.Text = exception.Message;
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
