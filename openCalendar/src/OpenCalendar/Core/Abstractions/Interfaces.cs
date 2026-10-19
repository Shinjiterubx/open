using OpenCalendar.Core.Models;

namespace OpenCalendar.Core.Abstractions;

/// <summary>All file system locations the app uses, in one place.</summary>
public interface IAppPaths
{
    string DataRoot { get; }
    string CalendarsDirectory { get; }
    string TrashDirectory { get; }
    string SettingsFile { get; }
    string LogDirectory { get; }
}

public interface ISettingsStore
{
    AppSettings Current { get; }

    void Save();

    event EventHandler? Saved;
}

/// <summary>Controls whether the app starts together with Windows.</summary>
public interface IAutoStartService
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}

/// <summary>Persists the user's calendars.</summary>
public interface ICalendarRepository
{
    IReadOnlyList<CalendarDocument> LoadAll();

    void Save(CalendarDocument calendar);

    /// <summary>Removes a calendar (implementations keep a recoverable copy).</summary>
    void Delete(Guid id);

    IReadOnlyList<string> LoadErrors { get; }
}

/// <summary>Reads and writes the portable <c>.opencalendar</c> file (one or more calendars).</summary>
public interface ICalendarSerializer
{
    void Write(IReadOnlyCollection<CalendarDocument> calendars, Stream destination);

    IReadOnlyList<CalendarDocument> Read(Stream source);
}

/// <summary>Read-only view of the openNotes note library (for synced deadlines and note links).</summary>
public interface INoteSource : IDisposable
{
    /// <summary>Folder that was found / configured, or <c>null</c> if openNotes data is not available.</summary>
    string? NotesFolder { get; }

    IReadOnlyList<NoteReference> Notes { get; }

    /// <summary>Raised (on a background thread) when notes were added, changed or removed.</summary>
    event EventHandler? NotesChanged;

    /// <summary>(Re)starts reading from the configured or detected folder.</summary>
    void Start(string? folderOverride);

    void Stop();
}

/// <summary>Read-only view of openKanban card deadlines (its <c>deadlines.json</c> feed).</summary>
public interface IKanbanSource : IDisposable
{
    bool IsAvailable { get; }

    IReadOnlyList<KanbanCardReference> Cards { get; }

    /// <summary>Raised (on a background thread) when the feed changed.</summary>
    event EventHandler? CardsChanged;

    void Start();

    void Stop();
}

/// <summary>Opens a card in openKanban.</summary>
public interface IKanbanLauncher
{
    bool TryOpenCard(Guid cardId);
}

/// <summary>Opens a note in the openNotes application.</summary>
public interface INoteLauncher
{
    bool IsAvailable { get; }

    /// <returns><c>false</c> if openNotes could not be found or started.</returns>
    bool TryOpen(Guid noteId);
}
