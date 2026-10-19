using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace OpenLauncher.UI.Converters;

/// <summary>
/// "#RRGGBB" → brush. ConverterParameter: "soft" (light tint for event blocks), "medium" (hover tint)
/// or nothing (solid colour). Brushes are cached and frozen.
/// </summary>
public sealed class HexToBrushConverter : IValueConverter
{
    private static readonly Dictionary<(string, string), SolidColorBrush> Cache = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = value as string ?? "#D2691E";
        var mode = parameter as string ?? "solid";
        if (Cache.TryGetValue((hex, mode), out var cached)) return cached;

        Color color;
        try { color = (Color)ColorConverter.ConvertFromString(hex); }
        catch (FormatException) { color = Color.FromRgb(0xD2, 0x69, 0x1E); }

        var brush = new SolidColorBrush(mode switch
        {
            "soft" => Blend(color, 0.16),
            "medium" => Blend(color, 0.28),
            "text" => Darken(color, 0.45),
            _ => color,
        });
        brush.Freeze();
        Cache[(hex, mode)] = brush;
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    /// <summary>Opaque mix of the colour over white (no transparency → crisp text on top).</summary>
    private static Color Blend(Color c, double amount) => Color.FromRgb(
        (byte)(255 - (255 - c.R) * amount),
        (byte)(255 - (255 - c.G) * amount),
        (byte)(255 - (255 - c.B) * amount));

    private static Color Darken(Color c, double amount) => Color.FromRgb(
        (byte)(c.R * (1 - amount)), (byte)(c.G * (1 - amount)), (byte)(c.B * (1 - amount)));
}
