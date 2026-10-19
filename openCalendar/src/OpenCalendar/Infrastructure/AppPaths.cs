using OpenCalendar.Core.Abstractions;

namespace OpenCalendar.Infrastructure;

/// <summary>
/// Calendars and settings: <c>%APPDATA%\openCalendar</c>; logs: <c>%LOCALAPPDATA%\openCalendar</c>.
/// Portable mode: an empty <c>portable.txt</c> next to <c>openCalendar.exe</c> keeps everything in <c>.\Data</c>.
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
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "openCalendar");
        var localRoot = isPortable
            ? Path.Combine(DataRoot, "Local")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "openCalendar");

        CalendarsDirectory = Path.Combine(DataRoot, "Calendars");
        TrashDirectory = Path.Combine(DataRoot, "Trash");
        SettingsFile = Path.Combine(DataRoot, "settings.json");
        LogDirectory = Path.Combine(localRoot, "Logs");

        foreach (var directory in new[] { DataRoot, CalendarsDirectory, TrashDirectory, LogDirectory })
            Directory.CreateDirectory(directory);
    }

    public string DataRoot { get; }
    public string CalendarsDirectory { get; }
    public string TrashDirectory { get; }
    public string SettingsFile { get; }
    public string LogDirectory { get; }
}
