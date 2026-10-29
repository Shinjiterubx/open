using OpenNotes.Core.Models;

namespace OpenNotes.Core.Library;

public enum NoteScopeKind
{
    /// <summary>Every note regardless of folder.</summary>
    All,

    /// <summary>Notes that are not in any folder.</summary>
    Unfiled,

    /// <summary>Notes of one folder.</summary>
    Folder,
}

/// <summary>Which part of the library is shown: all notes, notes without folder, or one folder.</summary>
public readonly record struct NoteScope(NoteScopeKind Kind, Guid? FolderId = null)
{
    public static NoteScope All { get; } = new(NoteScopeKind.All);

    public static NoteScope Unfiled { get; } = new(NoteScopeKind.Unfiled);

    public static NoteScope Of(Guid folderId) => new(NoteScopeKind.Folder, folderId);

    public bool Includes(Note note) => Kind switch
    {
        NoteScopeKind.All => true,
        NoteScopeKind.Unfiled => note.FolderId is null,
        _ => note.FolderId == FolderId,
    };
}

/// <summary>The list filter: open or closed notes, in a scope, matching a search text.</summary>
public static class NoteFilter
{
    public static bool IsVisible(Note note, bool showClosed, NoteScope scope, string? query) =>
        note.IsDone == showClosed && scope.Includes(note) && MatchesText(note, query);

    public static bool MatchesText(Note note, string? query) =>
        string.IsNullOrWhiteSpace(query)
        || note.Title.Contains(query.Trim(), StringComparison.CurrentCultureIgnoreCase)
        || note.Content.Contains(query.Trim(), StringComparison.CurrentCultureIgnoreCase);
}
