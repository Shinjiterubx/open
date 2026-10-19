using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenNotes.UI.Converters;

/// <summary>
/// Visible when the bound enum equals one of the values in ConverterParameter
/// (a comma separated list of names, e.g. "Editor,Split").
/// </summary>
public sealed class EnumToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var names = (parameter as string ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var match = value is not null && names.Contains(value.ToString(), StringComparer.Ordinal);
        return match ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
