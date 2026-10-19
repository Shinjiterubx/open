using Microsoft.Web.WebView2.Core;
using OpenNotes.Core.Abstractions;

namespace OpenNotes.UI.Controls;

/// <summary>
/// Shared WebView2 environment for every preview instance. Configured once by the composition root,
/// so the browser profile lives in a user-writable folder (never next to the exe in Program Files).
/// </summary>
public static class PreviewEnvironment
{
    private static Task<CoreWebView2Environment>? _environment;
    private static string? _userDataFolder;

    public static string WebRoot { get; private set; } = string.Empty;
    public static string NotesRoot { get; private set; } = string.Empty;

    public static void Configure(IAppPaths paths)
    {
        WebRoot = paths.WebAssetsDirectory;
        NotesRoot = paths.PreviewCacheDirectory;
        _userDataFolder = paths.WebViewDataDirectory;
    }

    public static Task<CoreWebView2Environment> GetAsync() =>
        _environment ??= CoreWebView2Environment.CreateAsync(browserExecutableFolder: null, userDataFolder: _userDataFolder);
}
