using System.Globalization;
using System.Windows;
using System.Windows.Data;
using OpenKanban.Core.Boards;

namespace OpenKanban.UI.Converters;

/// <summary>Due chip colours: overdue = red, soon = amber, done = green, upcoming = neutral. <see cref="Soft"/> = background tint.</summary>
public sealed class DueStateBrushConverter : IValueConverter
{
    public bool Soft { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as DueState? ?? DueState.None, Soft) switch
        {
            (DueState.Overdue, false) => "DangerBrush",
            (DueState.Overdue, true) => "DangerSoftBrush",
            (DueState.DueSoon, false) => "WarningBrush",
            (DueState.DueSoon, true) => "WarningSoftBrush",
            (DueState.Done, false) => "SuccessBrush",
            (DueState.Done, true) => "SuccessSoftBrush",
            (_, false) => "InkSecondaryBrush",
            (_, true) => "SurfaceSunkenBrush",
        };
        return Application.Current.FindResource(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>double − ConverterParameter (never below 0). Used to fit columns into the visible board height.</summary>
public sealed class SubtractConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var amount = double.TryParse(parameter as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var p) ? p : 0;
        return value is double d ? Math.Max(0, d - amount) : 0d;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
