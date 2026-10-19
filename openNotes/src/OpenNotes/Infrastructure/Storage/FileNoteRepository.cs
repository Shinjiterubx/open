using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Models;

namespace OpenNotes.Infrastructure.Storage;

/// <summary>
/// Stores each note as <c>{id}.openNote</c> in the notes directory.
/// Writes are atomic (temp file + replace); deleted notes are moved to the Trash folder, not destroyed.
/// </summary>
public sealed class FileNoteRepository : INoteRepository
{
    private readonly IAppPaths _paths;
    private readonly INoteSerializer _serializer;
    private readonly List<string> _loadErrors = new();

    public FileNoteRepository(IAppPaths paths, INoteSerializer serializer)
    {
        _paths = paths;
        _serializer = serializer;
    }

    public IReadOnlyList<string> LoadErrors => _loadErrors;

    public IReadOnlyList<Note> LoadAll()
    {
        _loadErrors.Clear();
        var notes = new List<Note>();

        foreach (var file in Directory.EnumerateFiles(_paths.NotesDirectory, "*" + OpenNoteSerializer.FileExtension))
        {
            try
            {
                using var stream = File.OpenRead(file);
                var note = _serializer.Read(stream);

                // The file name is the source of truth for identity inside the library.
                if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var idFromName))
                    note.Id = idFromName;

                notes.Add(note);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                _loadErrors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return notes;
    }

    public void Save(Note note)
    {
        var target = PathFor(note.Id);
        var temp = target + ".tmp";

        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            _serializer.Write(note, stream);
        }

        File.Move(temp, target, overwrite: true);
    }

    public void Delete(Guid id)
    {
        var source = PathFor(id);
        if (!File.Exists(source)) return;

        var trashName = $"{id:N}_{DateTime.Now:yyyyMMdd-HHmmss}{OpenNoteSerializer.FileExtension}";
        File.Move(source, Path.Combine(_paths.TrashDirectory, trashName), overwrite: true);
    }

    private string PathFor(Guid id) =>
        Path.Combine(_paths.NotesDirectory, id.ToString("N") + OpenNoteSerializer.FileExtension);
}
