using System.Globalization;
using System.Windows;
using System.Windows.Data;
using OpenCalendar.UI.ViewModels;

namespace OpenCalendar.UI.Converters;

/// <summary>Minutes from midnight → vertical pixels in the time grid.</summary>
public sealed class MinuteToPixelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double minutes ? minutes * TimeGrid.PixelsPerMinute : 0d;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>double → Thickness(0, value, 0, 0).</summary>
public sealed class TopMarginConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new Thickness(0, value is double top ? top : 0, 0, 0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Visible when the value is <c>null</c> (placeholders).</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
