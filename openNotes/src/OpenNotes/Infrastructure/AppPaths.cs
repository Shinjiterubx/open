using OpenNotes.Core.Abstractions;

namespace OpenNotes.Infrastructure;

/// <summary>
/// Default locations:
/// <list type="bullet">
/// <item>Notes and settings: <c>%APPDATA%\openNotes</c> (roams with the user profile in corporate environments).</item>
/// <item>Caches: <c>%LOCALAPPDATA%\openNotes</c>.</item>
/// </list>
/// Portable mode: put an empty <c>portable.txt</c> next to <c>openNotes.exe</c> and everything is stored in <c>.\Data</c>.
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
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "openNotes");

        var localRoot = isPortable
            ? Path.Combine(DataRoot, "Local")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "openNotes");

        NotesDirectory = Path.Combine(DataRoot, "Notes");
        TrashDirectory = Path.Combine(DataRoot, "Trash");
        SettingsFile = Path.Combine(DataRoot, "settings.json");
        FoldersFile = Path.Combine(DataRoot, "folders.json");
        LogDirectory = Path.Combine(localRoot, "Logs");
        PreviewCacheDirectory = Path.Combine(localRoot, "PreviewCache");
        WebViewDataDirectory = Path.Combine(localRoot, "WebView2");
        WebAssetsDirectory = Path.Combine(baseDirectory, "Web");

        foreach (var directory in new[] { DataRoot, NotesDirectory, TrashDirectory, LogDirectory, PreviewCacheDirectory, WebViewDataDirectory })
            Directory.CreateDirectory(directory);
    }

    public string DataRoot { get; }
    public string NotesDirectory { get; }
    public string TrashDirectory { get; }
    public string SettingsFile { get; }
    public string FoldersFile { get; }
    public string LogDirectory { get; }
    public string PreviewCacheDirectory { get; }
    public string WebViewDataDirectory { get; }
    public string WebAssetsDirectory { get; }
}
