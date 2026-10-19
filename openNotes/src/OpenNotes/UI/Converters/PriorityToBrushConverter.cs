using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using OpenNotes.Core.Models;

namespace OpenNotes.UI.Converters;

/// <summary>High → red, Medium → yellow, Low → purple, None → transparent.</summary>
public sealed class PriorityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        NotePriority.High => Find("PriorityHighBrush"),
        NotePriority.Medium => Find("PriorityMediumBrush"),
        NotePriority.Low => Find("PriorityLowBrush"),
        _ => Brushes.Transparent,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static object Find(string key) => Application.Current.FindResource(key);
}
