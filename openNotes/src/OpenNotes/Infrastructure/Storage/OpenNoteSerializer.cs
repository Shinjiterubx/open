using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Models;

namespace OpenNotes.Infrastructure.Storage;

/// <summary>
/// The <c>.openNote</c> format is a plain ZIP archive (see docs/FORMAT.md):
/// <code>
/// manifest.json   – metadata (format, version, id, title, dates, pinned, task state, folder)
/// content.md      – the note body as UTF-8 Markdown
/// assets/*        – embedded images referenced as assets/name.ext
/// </code>
/// </summary>
public sealed class OpenNoteSerializer : INoteSerializer
{
    public const string FileExtension = ".openNote";
    public const string FormatName = "openNote";
    public const int CurrentVersion = 1;

    private const string ManifestEntry = "manifest.json";
    private const string ContentEntry = "content.md";
    private const long MaxEntrySize = 200L * 1024 * 1024;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly HashSet<string> PreCompressedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".webp", ".avif", ".zip",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public void Write(Note note, Stream destination)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true, Utf8NoBom);

        var manifest = new OpenNoteManifest(FormatName, CurrentVersion, note.Id, note.Title,
            note.CreatedAt, note.UpdatedAt, note.IsPinned, $"openNotes {AppInfo.Version}",
            note.Priority == NotePriority.None ? null : note.Priority,
            note.DueDate, note.DueTime,
            note.ReminderEnabled ? true : null,
            note.IsDone ? true : null,
            note.FolderId,
            string.IsNullOrWhiteSpace(note.FolderName) ? null : note.FolderName);
        WriteText(archive, ManifestEntry, JsonSerializer.Serialize(manifest, JsonOptions));
        WriteText(archive, ContentEntry, note.Content);

        foreach (var (name, data) in note.Assets)
        {
            var level = PreCompressedExtensions.Contains(Path.GetExtension(name))
                ? CompressionLevel.NoCompression
                : CompressionLevel.Optimal;
            var entry = archive.CreateEntry($"{Note.AssetFolder}/{name}", level);
            using var stream = entry.Open();
            stream.Write(data);
        }
    }

    public Note Read(Stream source)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true, Utf8NoBom);

        var manifestEntry = archive.GetEntry(ManifestEntry)
            ?? throw new InvalidDataException("A fájl nem érvényes openNote jegyzet (hiányzik a manifest.json).");
        var manifest = JsonSerializer.Deserialize<OpenNoteManifest>(ReadText(manifestEntry), JsonOptions)
            ?? throw new InvalidDataException("Sérült manifest.json.");

        if (!string.Equals(manifest.Format, FormatName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Ismeretlen fájlformátum.");
        if (manifest.Version > CurrentVersion)
            throw new InvalidDataException($"A jegyzet egy újabb openNotes verzióval készült (formátum v{manifest.Version}).");

        var note = new Note
        {
            Id = manifest.Id == Guid.Empty ? Guid.NewGuid() : manifest.Id,
            Title = manifest.Title ?? string.Empty,
            CreatedAt = manifest.CreatedAt,
            UpdatedAt = manifest.UpdatedAt,
            IsPinned = manifest.IsPinned,
            Priority = manifest.Priority ?? NotePriority.None,
            DueDate = manifest.DueDate,
            DueTime = manifest.DueTime,
            ReminderEnabled = manifest.Reminder ?? false,
            IsDone = manifest.IsDone ?? false,
            FolderId = manifest.FolderId,
            FolderName = manifest.FolderName,
            Content = archive.GetEntry(ContentEntry) is { } contentEntry ? ReadText(contentEntry) : string.Empty,
        };

        foreach (var entry in archive.Entries)
        {
            var isAsset = entry.FullName.StartsWith(Note.AssetFolder + "/", StringComparison.OrdinalIgnoreCase)
                          && !string.IsNullOrEmpty(entry.Name);
            if (!isAsset) continue;
            if (entry.Length > MaxEntrySize) throw new InvalidDataException($"Túl nagy csatolmány: {entry.Name}");

            note.SetAsset(entry.Name, ReadBytes(entry));
        }

        return note;
    }

    private static void WriteText(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Utf8NoBom);
        writer.Write(text);
    }

    private static string ReadText(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxEntrySize) throw new InvalidDataException($"Túl nagy bejegyzés: {entry.Name}");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static byte[] ReadBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream((int)entry.Length);
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private sealed record OpenNoteManifest(
        string Format,
        int Version,
        Guid Id,
        string? Title,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        bool IsPinned,
        string? Generator,
        NotePriority? Priority = null,
        DateOnly? DueDate = null,
        TimeOnly? DueTime = null,
        bool? Reminder = null,
        bool? IsDone = null,
        Guid? FolderId = null,
        string? FolderName = null);
}
