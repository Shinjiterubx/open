using OpenKanban.Core.Models;

namespace OpenKanban.Core.Abstractions;

public interface IAppPaths
{
    string DataRoot { get; }
    string WorkspacesDirectory { get; }
    string TrashDirectory { get; }
    string SettingsFile { get; }
    string LogDirectory { get; }

    /// <summary>Flat list of cards with deadlines, read by openCalendar.</summary>
    string DeadlinesFile { get; }
}

public interface ISettingsStore
{
    AppSettings Current { get; }

    void Save();

    event EventHandler? Saved;
}

/// <summary>Persists workspaces (one file per workspace).</summary>
public interface IWorkspaceRepository
{
    IReadOnlyList<Workspace> LoadAll();

    void Save(Workspace workspace);

    void Delete(Guid id);

    IReadOnlyList<string> LoadErrors { get; }
}

/// <summary>Reads and writes the portable <c>.openKanban</c> file.</summary>
public interface IWorkspaceSerializer
{
    void Write(Workspace workspace, Stream destination);

    Workspace Read(Stream source);
}

/// <summary>Publishes card deadlines for other apps (openCalendar).</summary>
public interface IDeadlineFeed
{
    void Publish(IReadOnlyCollection<Workspace> workspaces);
}

/// <summary>Read-only list of openNotes notes (for linking).</summary>
public interface INoteSource
{
    bool IsAvailable { get; }

    IReadOnlyList<NoteReference> LoadNotes();
}

/// <summary>Opens things in the companion apps. Every method returns <c>false</c> if the app isn't installed.</summary>
public interface ICompanionLauncher
{
    bool IsOpenNotesAvailable { get; }
    bool IsOpenCalendarAvailable { get; }

    bool TryOpenNote(Guid noteId);

    bool TryShowInCalendar(DateOnly date);
}
