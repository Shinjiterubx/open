namespace OpenCalendar.Core.Models;

/// <summary>A card with a deadline read from openKanban's <c>deadlines.json</c> feed.</summary>
public sealed record KanbanCardReference(
    Guid CardId,
    string Title,
    DateOnly DueDate,
    TimeOnly? DueTime,
    bool IsDone,
    string WorkspaceName,
    string BoardName,
    string ColumnName,
    string Color)
{
    public string Context => $"{WorkspaceName} › {BoardName} › {ColumnName}";
}
