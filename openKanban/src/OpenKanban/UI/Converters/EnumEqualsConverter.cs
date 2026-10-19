using System.Globalization;
using System.Windows.Data;

namespace OpenKanban.UI.Converters;

/// <summary>Binds an enum property to a group of RadioButtons: ConverterParameter is the button's value.</summary>
public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && value.Equals(parameter);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null ? parameter : Binding.DoNothing;
}
