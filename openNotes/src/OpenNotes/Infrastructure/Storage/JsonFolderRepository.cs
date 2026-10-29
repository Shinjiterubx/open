using System.Text.Json;
using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Models;

namespace OpenNotes.Infrastructure.Storage;

/// <summary>
/// Stores the folder list as <c>folders.json</c> next to the notes (it travels with the library, e.g. in portable mode).
/// Notes keep only the folder id, so a missing or broken file never loses notes – they just show up without folder.
/// </summary>
public sealed class JsonFolderRepository : IFolderRepository
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _file;

    public JsonFolderRepository(IAppPaths paths) => _file = paths.FoldersFile;

    public IReadOnlyList<NoteFolder> LoadAll()
    {
        try
        {
            if (!File.Exists(_file)) return Array.Empty<NoteFolder>();
            var document = JsonSerializer.Deserialize<FolderDocument>(File.ReadAllText(_file), Options);
            return document?.Folders?.Where(f => f.Id != Guid.Empty && !string.IsNullOrWhiteSpace(f.Name)).ToList()
                   ?? (IReadOnlyList<NoteFolder>)Array.Empty<NoteFolder>();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return Array.Empty<NoteFolder>();
        }
    }

    public void SaveAll(IEnumerable<NoteFolder> folders)
    {
        var temp = _file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new FolderDocument { Folders = folders.ToList() }, Options));
        File.Move(temp, _file, overwrite: true);
    }

    private sealed class FolderDocument
    {
        public int Version { get; set; } = 1;
        public List<NoteFolder>? Folders { get; set; }
    }
}
