using OpenNotes.Core.Models;

namespace OpenNotes.Core.Reminders;

public enum ReminderState
{
    Overdue,
    Today,
    Upcoming,
}

/// <summary>A note with an active reminder, classified relative to "now".</summary>
public sealed record ReminderEntry(Note Note, ReminderState State, DateTime Due);

public sealed record ReminderCounts(int Overdue, int Today, int Upcoming)
{
    public int Total => Overdue + Today + Upcoming;
    public int NeedsAttention => Overdue + Today;
}

/// <summary>
/// Reminder semantics: a note reminds the user every day while it has a due date,
/// the reminder is on and it isn't marked done – before, on and after the due date.
/// </summary>
public static class ReminderRules
{
    /// <summary>A due date without a time is due at the end of that day.</summary>
    public static readonly TimeOnly EndOfDay = new(23, 59, 59);

    public static bool IsActive(Note note) => note.ReminderEnabled && !note.IsDone && note.DueDate is not null;

    public static DateTime? DueMoment(Note note) => note.DueDate?.ToDateTime(note.DueTime ?? EndOfDay);

    public static ReminderState Classify(DateTime due, DateTime now) =>
        due < now ? ReminderState.Overdue
        : due.Date == now.Date ? ReminderState.Today
        : ReminderState.Upcoming;

    /// <summary>Active reminders ordered: overdue → today → upcoming, then priority (high first), then due date.</summary>
    public static IReadOnlyList<ReminderEntry> Collect(IEnumerable<Note> notes, DateTime now) =>
        notes.Where(IsActive)
            .Select(n =>
            {
                var due = DueMoment(n)!.Value;
                return new ReminderEntry(n, Classify(due, now), due);
            })
            .OrderBy(e => e.State)
            .ThenByDescending(e => e.Note.Priority)
            .ThenBy(e => e.Due)
            .ToList();

    public static ReminderCounts Count(IReadOnlyCollection<ReminderEntry> entries) =>
        new(entries.Count(e => e.State == ReminderState.Overdue),
            entries.Count(e => e.State == ReminderState.Today),
            entries.Count(e => e.State == ReminderState.Upcoming));
}
