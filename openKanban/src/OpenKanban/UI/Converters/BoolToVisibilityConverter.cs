using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenKanban.UI.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        return flag ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility visibility && (visibility == Visibility.Visible) ^ Invert;
}
