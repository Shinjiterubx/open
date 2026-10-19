using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenCalendar.Core.Abstractions;
using OpenCalendar.Core.Models;

namespace OpenCalendar.Infrastructure.Integration;

/// <summary>
/// Read-only sync with openNotes: reads the <c>manifest.json</c> of every <c>.openNote</c> file in the
/// openNotes library and watches the folder, so deadlines set in openNotes appear in the calendar automatically.
/// Notes are never modified from here.
/// </summary>
public sealed class OpenNotesLibrary : INoteSource
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(800);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly object _gate = new();
    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private IReadOnlyList<NoteReference> _notes = Array.Empty<NoteReference>();

    public string? NotesFolder { get; private set; }

    public IReadOnlyList<NoteReference> Notes
    {
        get { lock (_gate) return _notes; }
    }

    public event EventHandler? NotesChanged;

    public void Start(string? folderOverride)
    {
        Stop();

        NotesFolder = !string.IsNullOrWhiteSpace(folderOverride) && Directory.Exists(folderOverride)
            ? folderOverride
            : OpenNotesLocator.FindNotesFolder();

        if (NotesFolder is null)
        {
            SetNotes(Array.Empty<NoteReference>());
            return;
        }

        Reload();

        _debounceTimer = new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
        _watcher = new FileSystemWatcher(NotesFolder, "*.openNote")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            IncludeSubdirectories = false,
        };
        _watcher.Created += OnFolderChanged;
        _watcher.Changed += OnFolderChanged;
        _watcher.Deleted += OnFolderChanged;
        _watcher.Renamed += OnFolderChanged;
        _watcher.Error += (_, _) => ScheduleReload();
        _watcher.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;
        _debounceTimer?.Dispose();
        _debounceTimer = null;
    }

    public void Dispose() => Stop();

    private void OnFolderChanged(object sender, FileSystemEventArgs e) => ScheduleReload();

    /// <summary>openNotes writes via temp file + rename, so several events arrive together: reload once.</summary>
    private void ScheduleReload() => _debounceTimer?.Change(Debounce, Timeout.InfiniteTimeSpan);

    private void Reload()
    {
        var folder = NotesFolder;
        if (folder is null || !Directory.Exists(folder)) return;

        var notes = new List<NoteReference>();
        foreach (var file in Directory.EnumerateFiles(folder, "*.openNote"))
        {
            if (TryRead(file) is { } note) notes.Add(note);
        }
        SetNotes(notes.OrderBy(n => n.DisplayTitle, StringComparer.CurrentCultureIgnoreCase).ToList());
    }

    private void SetNotes(IReadOnlyList<NoteReference> notes)
    {
        lock (_gate) _notes = notes;
        NotesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static NoteReference? TryRead(string file)
    {
        // The file may be mid-write by openNotes; a later change event triggers another reload.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                var entry = archive.GetEntry("manifest.json");
                if (entry is null) return null;

                using var manifestStream = entry.Open();
                var manifest = JsonSerializer.Deserialize<NoteManifest>(manifestStream, JsonOptions);
                if (manifest is null || manifest.Id == Guid.Empty) return null;

                var id = Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var fromName) ? fromName : manifest.Id;
                return new NoteReference(id, manifest.Title ?? string.Empty, manifest.DueDate, manifest.DueTime,
                    manifest.IsDone ?? false, manifest.Reminder ?? false, manifest.Priority ?? NotePriority.None, manifest.UpdatedAt);
            }
            catch (IOException)
            {
                Thread.Sleep(150);
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException or UnauthorizedAccessException)
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>Subset of the openNotes manifest (see openNotes docs/FORMAT.md).</summary>
    private sealed record NoteManifest(
        Guid Id,
        string? Title,
        DateTimeOffset UpdatedAt,
        NotePriority? Priority,
        DateOnly? DueDate,
        TimeOnly? DueTime,
        bool? Reminder,
        bool? IsDone);
}
