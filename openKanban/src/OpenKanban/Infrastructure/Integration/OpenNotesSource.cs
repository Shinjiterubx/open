using System.IO.Compression;
using System.Text.Json;
using OpenKanban.Core.Abstractions;
using OpenKanban.Core.Models;

namespace OpenKanban.Infrastructure.Integration;

/// <summary>Reads note titles from the openNotes library (manifest.json of each .openNote). Never writes.</summary>
public sealed class OpenNotesSource : INoteSource
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public bool IsAvailable => FindFolder() is not null;

    public IReadOnlyList<NoteReference> LoadNotes()
    {
        var folder = FindFolder();
        if (folder is null) return Array.Empty<NoteReference>();

        var notes = new List<NoteReference>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.openNote"))
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                if (archive.GetEntry("manifest.json") is not { } entry) continue;

                using var manifestStream = entry.Open();
                var manifest = JsonSerializer.Deserialize<NoteManifest>(manifestStream, JsonOptions);
                if (manifest is null) continue;

                var id = Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var fromName) ? fromName : manifest.Id;
                notes.Add(new NoteReference(id, manifest.Title ?? string.Empty));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException)
            {
                // Skip unreadable notes.
            }
        }

        return notes.OrderBy(n => n.DisplayTitle, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static string? FindFolder()
    {
        var fromRegistry = AppRegistry.Read("openNotes", "NotesDirectory");
        if (fromRegistry is not null && Directory.Exists(fromRegistry)) return fromRegistry;

        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "openNotes", "Notes");
        return Directory.Exists(fallback) ? fallback : null;
    }

    private sealed record NoteManifest(Guid Id, string? Title);
}
