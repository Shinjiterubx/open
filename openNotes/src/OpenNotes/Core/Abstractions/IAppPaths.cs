namespace OpenNotes.Core.Abstractions;

/// <summary>All file system locations the app uses, in one place.</summary>
public interface IAppPaths
{
    string DataRoot { get; }
    string NotesDirectory { get; }
    string TrashDirectory { get; }
    string SettingsFile { get; }
    string LogDirectory { get; }
    string PreviewCacheDirectory { get; }
    string WebViewDataDirectory { get; }
    string WebAssetsDirectory { get; }
}
