using OpenCalendar.Core.Models;

namespace OpenCalendar.Core.Recurrence;

/// <summary>One concrete instance of a (possibly recurring) event.</summary>
public sealed record Occurrence(CalendarEvent Event, CalendarDocument Calendar, DateTime Start, DateTime End)
{
    public bool IsAllDay => Event.IsAllDay;

    /// <summary>Unique per event and occurrence (used for reminders and selection).</summary>
    public string Key => $"{Event.Id:N}@{Start:yyyyMMddHHmm}";
}
