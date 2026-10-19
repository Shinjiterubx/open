using OpenCalendar.Core.Models;
using OpenCalendar.Core.Recurrence;

namespace OpenCalendar.Core.Agenda;

/// <summary>Merges calendar events and openNotes deadlines into one sorted list of <see cref="CalendarEntry"/>.</summary>
public static class AgendaBuilder
{
    public const string NotesColor = "#5B3E96";

    /// <summary>Length of a timed openNotes deadline in the time grid.</summary>
    public static readonly TimeSpan NoteBlockLength = TimeSpan.FromMinutes(30);

    public static IReadOnlyList<CalendarEntry> Build(
        IEnumerable<CalendarDocument> visibleCalendars,
        IEnumerable<NoteReference> notes,
        bool includeDoneNotes,
        DateTime from,
        DateTime to,
        IEnumerable<KanbanCardReference>? kanbanCards = null)
    {
        var entries = OccurrenceExpander.Expand(visibleCalendars, from, to).Select(FromOccurrence).ToList();

        foreach (var note in notes)
        {
            if (FromNote(note, includeDoneNotes) is { } entry && entry.Overlaps(from, to))
                entries.Add(entry);
        }

        foreach (var card in kanbanCards ?? Array.Empty<KanbanCardReference>())
        {
            if (FromKanbanCard(card, includeDoneNotes) is { } entry && entry.Overlaps(from, to))
                entries.Add(entry);
        }

        return entries
            .OrderBy(e => e.Start.Date)
            .ThenByDescending(e => e.IsAllDay)
            .ThenBy(e => e.Start)
            .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static CalendarEntry FromOccurrence(Occurrence o) => new(
        o.Key, EntryKind.Event, o.Event.DisplayTitle, o.Start, o.IsAllDay ? o.Start.Date + (o.Event.End.Date - o.Event.Start.Date) : o.End,
        o.IsAllDay, o.Calendar.Color, o.Event.Location, o.Event.IsRecurring, o.Event.Note?.NoteId,
        NotePriority.None, false, o, null);

    public const string KanbanColor = "#2F5A8A";

    public static CalendarEntry? FromKanbanCard(KanbanCardReference card, bool includeDone)
    {
        if (card.IsDone && !includeDone) return null;

        var allDay = card.DueTime is null;
        var start = allDay ? card.DueDate.ToDateTime(TimeOnly.MinValue) : card.DueDate.ToDateTime(card.DueTime!.Value);
        var end = allDay ? start : start + NoteBlockLength;

        return new CalendarEntry($"kanban:{card.CardId:N}", EntryKind.KanbanCard, card.Title, start, end, allDay,
            KanbanColor, card.Context, false, null, NotePriority.None, card.IsDone, null, null);
    }

    public static CalendarEntry? FromNote(NoteReference note, bool includeDone)
    {
        if (note.DueDate is not { } date || (note.IsDone && !includeDone)) return null;

        var allDay = note.DueTime is null;
        var start = allDay ? date.ToDateTime(TimeOnly.MinValue) : date.ToDateTime(note.DueTime!.Value);
        var end = allDay ? start : start + NoteBlockLength;

        return new CalendarEntry($"note:{note.Id:N}", EntryKind.NoteDeadline, note.DisplayTitle, start, end, allDay,
            NotesColor, null, false, note.Id, note.Priority, note.IsDone, null, note);
    }
}
