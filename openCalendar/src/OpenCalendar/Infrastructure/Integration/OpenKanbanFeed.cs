using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using OpenCalendar.Core.Abstractions;
using OpenCalendar.Core.Models;

namespace OpenCalendar.Infrastructure.Integration;

/// <summary>
/// Reads openKanban's <c>deadlines.json</c> (path from <c>HKCU\Software\openKanban\DeadlinesFile</c>, or the default
/// <c>%APPDATA%\openKanban\deadlines.json</c>) and watches it. Read-only; openKanban is not required.
/// </summary>
public sealed class OpenKanbanFeed : IKanbanSource
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(600);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly object _gate = new();
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private string? _file;
    private IReadOnlyList<KanbanCardReference> _cards = Array.Empty<KanbanCardReference>();

    public bool IsAvailable => FindFeedFile() is { } file && File.Exists(file);

    public IReadOnlyList<KanbanCardReference> Cards
    {
        get { lock (_gate) return _cards; }
    }

    public event EventHandler? CardsChanged;

    public void Start()
    {
        Stop();
        _file = FindFeedFile();
        if (_file is null) return;

        var directory = Path.GetDirectoryName(_file);
        if (directory is null || !Directory.Exists(directory))
        {
            SetCards(Array.Empty<KanbanCardReference>());
            return;
        }

        Reload();
        _debounce = new Timer(_ => Reload(), null, Timeout.Infinite, Timeout.Infinite);
        _watcher = new FileSystemWatcher(directory, Path.GetFileName(_file))
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Changed += (_, _) => Schedule();
        _watcher.Created += (_, _) => Schedule();
        _watcher.Renamed += (_, _) => Schedule();
        _watcher.Deleted += (_, _) => Schedule();
        _watcher.EnableRaisingEvents = true;
    }

    public void Stop()
    {
        _watcher?.Dispose();
        _watcher = null;
        _debounce?.Dispose();
        _debounce = null;
    }

    public void Dispose() => Stop();

    private void Schedule() => _debounce?.Change(Debounce, Timeout.InfiniteTimeSpan);

    private void Reload()
    {
        if (_file is null) return;
        if (!File.Exists(_file))
        {
            SetCards(Array.Empty<KanbanCardReference>());
            return;
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var stream = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var feed = JsonSerializer.Deserialize<Feed>(stream, JsonOptions);
                SetCards(feed?.Cards?.Where(c => c.CardId != Guid.Empty).ToList() ?? new List<KanbanCardReference>());
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(150);
            }
            catch (Exception ex) when (ex is JsonException or UnauthorizedAccessException)
            {
                return;   // keep the last good data
            }
        }
    }

    private void SetCards(IReadOnlyList<KanbanCardReference> cards)
    {
        lock (_gate) _cards = cards;
        CardsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string? FindFeedFile()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\openKanban");
            if (key?.GetValue("DeadlinesFile") is string path && !string.IsNullOrWhiteSpace(path)) return path;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // fall back to the default location
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "openKanban", "deadlines.json");
    }

    private sealed record Feed(string? Format, int Version, List<KanbanCardReference>? Cards);
}
