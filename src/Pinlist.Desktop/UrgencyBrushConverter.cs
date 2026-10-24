using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Pinlist.Core;

namespace Pinlist.Desktop;

public sealed class UrgencyBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        TaskUrgency.High => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 103, 113)),
        TaskUrgency.Normal => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 190, 92)),
        _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 179, 255))
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
