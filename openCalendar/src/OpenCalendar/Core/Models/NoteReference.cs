namespace OpenCalendar.Core.Models;

public enum NotePriority
{
    None,
    Low,
    Medium,
    High,
}

/// <summary>A note read from the openNotes library (metadata only).</summary>
public sealed record NoteReference(
    Guid Id,
    string Title,
    DateOnly? DueDate,
    TimeOnly? DueTime,
    bool IsDone,
    bool ReminderEnabled,
    NotePriority Priority,
    DateTimeOffset UpdatedAt)
{
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "Névtelen jegyzet" : Title.Trim();
}
