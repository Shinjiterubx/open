using OpenCalendar.Core.Models;
using OpenCalendar.Core.Recurrence;

namespace OpenCalendar.Core.Agenda;

public enum EntryKind
{
    Event,
    NoteDeadline,
    KanbanCard,
}

/// <summary>
/// Anything shown in the calendar: an event occurrence or an openNotes deadline. UI-independent.
/// </summary>
public sealed record CalendarEntry(
    string Key,
    EntryKind Kind,
    string Title,
    DateTime Start,
    DateTime End,
    bool IsAllDay,
    string Color,
    string? Location,
    bool IsRecurring,
    Guid? LinkedNoteId,
    NotePriority Priority,
    bool IsDone,
    Occurrence? Occurrence,
    NoteReference? Note)
{
    /// <summary>All-day entries cover whole days: [Start.Date, End.Date + 1).</summary>
    public DateTime EffectiveEnd => IsAllDay ? End.Date.AddDays(1) : End;

    public bool Overlaps(DateTime from, DateTime to) => Start < to && EffectiveEnd > from;

    public bool IsOnDay(DateOnly day)
    {
        var from = day.ToDateTime(TimeOnly.MinValue);
        return Overlaps(from, from.AddDays(1));
    }
}
