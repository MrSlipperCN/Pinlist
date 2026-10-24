using System.Globalization;
using System.Windows.Data;
using Pinlist.Core;

namespace Pinlist.Desktop;

public sealed class TaskContextDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? UiText.Get("NoDetails") : value;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class TaskStatusTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        PinTaskStatus.Open => UiText.Get("StatusOpen"),
        PinTaskStatus.Closed => UiText.Get("StatusClosed"),
        PinTaskStatus.Trashed => UiText.Get("StatusTrashed"),
        _ => string.Empty
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
