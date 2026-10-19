using OpenKanban.Core.Abstractions;

namespace OpenKanban.Infrastructure;

/// <summary>
/// Workspaces and settings: <c>%APPDATA%\openKanban</c>; logs: <c>%LOCALAPPDATA%\openKanban</c>.
/// Portable mode: an empty <c>portable.txt</c> next to <c>openKanban.exe</c> keeps everything in <c>.\Data</c>.
/// </summary>
public sealed class AppPaths : IAppPaths
{
    public const string PortableMarkerFile = "portable.txt";

    public AppPaths()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var isPortable = File.Exists(Path.Combine(baseDirectory, PortableMarkerFile));

        DataRoot = isPortable
            ? Path.Combine(baseDirectory, "Data")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "openKanban");
        var localRoot = isPortable
            ? Path.Combine(DataRoot, "Local")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "openKanban");

        WorkspacesDirectory = Path.Combine(DataRoot, "Workspaces");
        TrashDirectory = Path.Combine(DataRoot, "Trash");
        SettingsFile = Path.Combine(DataRoot, "settings.json");
        DeadlinesFile = Path.Combine(DataRoot, "deadlines.json");
        LogDirectory = Path.Combine(localRoot, "Logs");

        foreach (var directory in new[] { DataRoot, WorkspacesDirectory, TrashDirectory, LogDirectory })
            Directory.CreateDirectory(directory);
    }

    public string DataRoot { get; }
    public string WorkspacesDirectory { get; }
    public string TrashDirectory { get; }
    public string SettingsFile { get; }
    public string LogDirectory { get; }
    public string DeadlinesFile { get; }
}
