using System.Text.Json.Serialization;

namespace OpenCalendar.Core.Models;

/// <summary>
/// A calendar entry. Times are local "wall clock" times (like most desktop calendars):
/// an event at 09:00 stays at 09:00 regardless of daylight saving changes.
/// </summary>
public sealed class CalendarEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Description { get; set; }

    /// <summary>Start of the (first) occurrence. For all-day events only the date is used.</summary>
    public DateTime Start { get; set; }

    /// <summary>End of the (first) occurrence; for all-day events the last day (inclusive).</summary>
    public DateTime End { get; set; }

    public bool IsAllDay { get; set; }

    /// <summary><c>null</c> = single event.</summary>
    public RecurrenceRule? Recurrence { get; set; }

    /// <summary>Occurrence dates removed from a recurring series ("delete only this one").</summary>
    public List<DateOnly> ExceptionDates { get; set; } = new();

    /// <summary>Reminder offsets in minutes before the start (0 = at start). Every occurrence reminds.</summary>
    public List<int> ReminderMinutes { get; set; } = new();

    /// <summary>Optional link to an openNotes note.</summary>
    public LinkedNote? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "(Névtelen esemény)" : Title.Trim();

    [JsonIgnore]
    public bool IsRecurring => Recurrence is { Frequency: not RecurrenceFrequency.None };

    /// <summary>Length of one occurrence (at least one minute; all-day events span whole days).</summary>
    [JsonIgnore]
    public TimeSpan Duration
    {
        get
        {
            if (IsAllDay) return End.Date.AddDays(1) - Start.Date;
            var duration = End - Start;
            return duration > TimeSpan.Zero ? duration : TimeSpan.FromMinutes(1);
        }
    }

    public CalendarEvent Clone()
    {
        var copy = (CalendarEvent)MemberwiseClone();
        copy.Recurrence = Recurrence?.Clone();
        copy.ExceptionDates = new List<DateOnly>(ExceptionDates);
        copy.ReminderMinutes = new List<int>(ReminderMinutes);
        return copy;
    }
}

/// <summary>Reference to a note in openNotes (title cached for display).</summary>
public sealed record LinkedNote(Guid NoteId, string Title);
