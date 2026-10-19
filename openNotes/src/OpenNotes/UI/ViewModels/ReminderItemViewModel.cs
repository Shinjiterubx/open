using OpenNotes.Core.Models;
using OpenNotes.Core.Reminders;

namespace OpenNotes.UI.ViewModels;

/// <summary>One row in the "Teendők" panel / summary window (immutable snapshot, rebuilt on refresh).</summary>
public sealed class ReminderItemViewModel
{
    public ReminderItemViewModel(NoteItemViewModel note, ReminderEntry entry, DateTime now)
    {
        Note = note;
        State = entry.State;
        Priority = entry.Note.Priority;
        Title = note.Title;
        DueText = DueDateText.Relative(entry.Note, now);
        Snippet = note.Snippet;
    }

    public NoteItemViewModel Note { get; }
    public ReminderState State { get; }
    public NotePriority Priority { get; }
    public bool HasPriority => Priority != NotePriority.None;
    public string Title { get; }
    public string DueText { get; }
    public string Snippet { get; }

    /// <summary>Identity used to skip needless list rebuilds.</summary>
    internal string Signature => $"{Note.Id:N}|{State}|{Priority}|{Title}|{DueText}";
}
