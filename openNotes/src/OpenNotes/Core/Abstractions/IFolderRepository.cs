using OpenNotes.Core.Models;

namespace OpenNotes.Core.Abstractions;

/// <summary>Persists the list of note folders (order, names and colours).</summary>
public interface IFolderRepository
{
    IReadOnlyList<NoteFolder> LoadAll();

    void SaveAll(IEnumerable<NoteFolder> folders);
}
