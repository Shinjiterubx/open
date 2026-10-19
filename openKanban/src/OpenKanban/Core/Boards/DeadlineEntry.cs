namespace OpenKanban.Core.Boards;

/// <summary>
/// One card with a deadline in the shared <c>deadlines.json</c> feed (read by openCalendar).
/// Keep this contract backwards compatible: only add optional fields.
/// </summary>
public sealed record DeadlineEntry(
    Guid CardId,
    string Title,
    DateOnly DueDate,
    TimeOnly? DueTime,
    bool IsDone,
    string WorkspaceName,
    string BoardName,
    string ColumnName,
    string Color);

public sealed record DeadlineFeedDocument(string Format, int Version, DateTimeOffset GeneratedAt, IReadOnlyList<DeadlineEntry> Cards);
