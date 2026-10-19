using System.Globalization;
using System.Windows;
using System.Windows.Data;
using OpenNotes.Core.Reminders;

namespace OpenNotes.UI.Converters;

/// <summary>
/// Colour of a due date: overdue → red, today → accent purple, upcoming / none → muted.
/// With <see cref="Soft"/> it returns the matching light background instead.
/// </summary>
public sealed class ReminderStateToBrushConverter : IValueConverter
{
    public bool Soft { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as ReminderState?, Soft) switch
        {
            (ReminderState.Overdue, false) => "DangerBrush",
            (ReminderState.Overdue, true) => "DangerSoftBrush",
            (ReminderState.Today, false) => "AccentBrush",
            (ReminderState.Today, true) => "AccentSoftBrush",
            (_, false) => "InkMutedBrush",
            (_, true) => "SurfaceSunkenBrush",
        };
        return Application.Current.FindResource(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
