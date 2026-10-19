using OpenNotes.Core.Models;

namespace OpenNotes.Core.Abstractions;

/// <summary>Persists the user's note library.</summary>
public interface INoteRepository
{
    /// <summary>Loads every readable note. Unreadable files are skipped (and reported via <see cref="LoadErrors"/>).</summary>
    IReadOnlyList<Note> LoadAll();

    void Save(Note note);

    /// <summary>Removes a note from the library (implementations may keep a recoverable copy).</summary>
    void Delete(Guid id);

    IReadOnlyList<string> LoadErrors { get; }
}
