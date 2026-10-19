using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Models;

namespace OpenNotes.Infrastructure.Rendering;

/// <summary>Virtual host names the WebView2 preview uses.</summary>
public static class PreviewHosts
{
    /// <summary>Serves the shell page, CSS and fonts from the install directory.</summary>
    public const string App = "app.opennotes";

    /// <summary>Serves note images from the preview cache.</summary>
    public const string Notes = "notes.opennotes";
}

/// <summary>
/// Mirrors note assets to <c>PreviewCache\{noteId}\assets</c> so the preview can load them
/// through the <see cref="PreviewHosts.Notes"/> virtual host.
/// </summary>
public sealed class PreviewAssetCache : IPreviewAssetCache
{
    private readonly string _root;

    public PreviewAssetCache(IAppPaths paths) => _root = paths.PreviewCacheDirectory;

    public void Reset()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Another process may hold a file; stale cache entries are harmless.
        }
        Directory.CreateDirectory(_root);
    }

    public void Sync(Note note)
    {
        if (note.Assets.Count == 0) return;

        var directory = Path.Combine(_root, note.Id.ToString("N"), Note.AssetFolder);
        Directory.CreateDirectory(directory);

        foreach (var (name, data) in note.Assets)
        {
            var path = Path.Combine(directory, name);
            var info = new FileInfo(path);
            if (info.Exists && info.Length == data.Length) continue;
            File.WriteAllBytes(path, data);
        }
    }

    public string RewriteUrl(Guid noteId, string url) =>
        AssetName.IsAssetPath(url) ? $"https://{PreviewHosts.Notes}/{noteId:N}/{url}" : url;
}
