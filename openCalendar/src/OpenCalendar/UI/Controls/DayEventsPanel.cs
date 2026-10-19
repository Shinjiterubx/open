using System.Windows;
using System.Windows.Controls;
using OpenCalendar.UI.ViewModels;

namespace OpenCalendar.UI.Controls;

/// <summary>
/// Lays out timed entries in a day column: vertical position from start/end minute,
/// horizontal position from the overlap column (see Core.Agenda.DayLayout).
/// </summary>
public sealed class DayEventsPanel : Panel
{
    private const double Gap = 2;

    public static readonly DependencyProperty StartMinuteProperty = DependencyProperty.RegisterAttached(
        "StartMinute", typeof(double), typeof(DayEventsPanel), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static readonly DependencyProperty EndMinuteProperty = DependencyProperty.RegisterAttached(
        "EndMinute", typeof(double), typeof(DayEventsPanel), new FrameworkPropertyMetadata(60d, FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static readonly DependencyProperty ColumnProperty = DependencyProperty.RegisterAttached(
        "Column", typeof(int), typeof(DayEventsPanel), new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static readonly DependencyProperty ColumnCountProperty = DependencyProperty.RegisterAttached(
        "ColumnCount", typeof(int), typeof(DayEventsPanel), new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsParentArrange));

    public static double GetStartMinute(DependencyObject d) => (double)d.GetValue(StartMinuteProperty);
    public static void SetStartMinute(DependencyObject d, double value) => d.SetValue(StartMinuteProperty, value);
    public static double GetEndMinute(DependencyObject d) => (double)d.GetValue(EndMinuteProperty);
    public static void SetEndMinute(DependencyObject d, double value) => d.SetValue(EndMinuteProperty, value);
    public static int GetColumn(DependencyObject d) => (int)d.GetValue(ColumnProperty);
    public static void SetColumn(DependencyObject d, int value) => d.SetValue(ColumnProperty, value);
    public static int GetColumnCount(DependencyObject d) => (int)d.GetValue(ColumnCountProperty);
    public static void SetColumnCount(DependencyObject d, int value) => d.SetValue(ColumnCountProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width;
        foreach (UIElement child in InternalChildren)
        {
            var (_, _, w, h) = Slot(child, width);
            child.Measure(new Size(w, h));
        }
        return new Size(width, TimeGrid.TotalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (UIElement child in InternalChildren)
        {
            var (x, y, w, h) = Slot(child, finalSize.Width);
            child.Arrange(new Rect(x, y, w, h));
        }
        return finalSize;
    }

    private static (double X, double Y, double Width, double Height) Slot(UIElement child, double width)
    {
        var count = Math.Max(1, GetColumnCount(child));
        var column = Math.Clamp(GetColumn(child), 0, count - 1);
        var usable = Math.Max(0, width - Gap * 3);
        var columnWidth = usable / count;

        var y = GetStartMinute(child) * TimeGrid.PixelsPerMinute;
        var height = Math.Max(18, (GetEndMinute(child) - GetStartMinute(child)) * TimeGrid.PixelsPerMinute - 1);
        return (Gap + column * columnWidth, y, Math.Max(0, columnWidth - Gap), height);
    }
}
