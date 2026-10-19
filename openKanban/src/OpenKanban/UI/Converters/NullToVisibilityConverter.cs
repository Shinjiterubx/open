using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenKanban.UI.Converters;

/// <summary>Visible when the value is <c>null</c> or an empty string (placeholders). Invert: visible when set.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isEmpty = value is null || value is string { Length: 0 };
        return isEmpty ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
