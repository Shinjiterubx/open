using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using OpenNotes.Infrastructure.Rendering;

namespace OpenNotes.UI.Controls;

/// <summary>
/// Rendered note preview (WebView2). Shows <see cref="Html"/> inside a styled shell page and reports
/// task checkbox clicks through <see cref="ToggleTaskCommand"/> (parameter: task index).
/// External links open in the default browser.
/// </summary>
public sealed class MarkdownPreview : Border
{
    public static readonly DependencyProperty HtmlProperty = DependencyProperty.Register(
        nameof(Html), typeof(string), typeof(MarkdownPreview), new PropertyMetadata(string.Empty, (d, _) => ((MarkdownPreview)d).PushHtml()));

    public static readonly DependencyProperty ToggleTaskCommandProperty = DependencyProperty.Register(
        nameof(ToggleTaskCommand), typeof(ICommand), typeof(MarkdownPreview));

    public static readonly DependencyProperty OpenImageCommandProperty = DependencyProperty.Register(
        nameof(OpenImageCommand), typeof(ICommand), typeof(MarkdownPreview));

    public static readonly DependencyProperty ContentFontSizeProperty = DependencyProperty.Register(
        nameof(ContentFontSize), typeof(double), typeof(MarkdownPreview), new PropertyMetadata(15d, (d, _) => ((MarkdownPreview)d).PushFontSize()));

    public static readonly DependencyProperty CompactProperty = DependencyProperty.Register(
        nameof(Compact), typeof(bool), typeof(MarkdownPreview), new PropertyMetadata(false));

    private static readonly string ShellUrl = $"https://{PreviewHosts.App}/shell.html";

    private readonly WebView2 _webView = new();
    private bool _initializationStarted;
    private bool _isReady;

    public MarkdownPreview()
    {
        Background = (System.Windows.Media.Brush)Application.Current.FindResource("SurfaceBrush");
        _webView.DefaultBackgroundColor = System.Drawing.Color.White;
        Child = _webView;

        Loaded += (_, _) => _ = InitializeAsync();
        IsVisibleChanged += (_, _) => { if (IsVisible) _ = InitializeAsync(); };
    }

    public string Html
    {
        get => (string)GetValue(HtmlProperty);
        set => SetValue(HtmlProperty, value);
    }

    public ICommand? ToggleTaskCommand
    {
        get => (ICommand?)GetValue(ToggleTaskCommandProperty);
        set => SetValue(ToggleTaskCommandProperty, value);
    }

    /// <summary>Executed with the Markdown path (<c>assets/x.png</c>) of a clicked embedded image.</summary>
    public ICommand? OpenImageCommand
    {
        get => (ICommand?)GetValue(OpenImageCommandProperty);
        set => SetValue(OpenImageCommandProperty, value);
    }

    public double ContentFontSize
    {
        get => (double)GetValue(ContentFontSizeProperty);
        set => SetValue(ContentFontSizeProperty, value);
    }

    /// <summary>Tighter margins for the mini window.</summary>
    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    private async Task InitializeAsync()
    {
        if (_initializationStarted || !IsVisible) return;
        _initializationStarted = true;

        try
        {
            var environment = await PreviewEnvironment.GetAsync();
            await _webView.EnsureCoreWebView2Async(environment);

            var core = _webView.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping(PreviewHosts.App, PreviewEnvironment.WebRoot, CoreWebView2HostResourceAccessKind.Allow);
            core.SetVirtualHostNameToFolderMapping(PreviewHosts.Notes, PreviewEnvironment.NotesRoot, CoreWebView2HostResourceAccessKind.Allow);

            var settings = core.Settings;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = false;
            settings.AreBrowserAcceleratorKeysEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
            settings.IsPasswordAutosaveEnabled = false;
#if !DEBUG
            settings.AreDevToolsEnabled = false;
#endif

            core.WebMessageReceived += OnWebMessageReceived;
            core.NavigationStarting += OnNavigationStarting;
            core.NewWindowRequested += OnNewWindowRequested;
            core.Navigate(ShellUrl);
        }
        catch (Exception ex) when (ex is WebView2RuntimeNotFoundException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            Child = new TextBlock
            {
                Text = "Az előnézethez a Microsoft Edge WebView2 Runtime szükséges.\nTöltsd le: https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(24),
                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("InkMutedBrush"),
            };
        }
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var message = JsonDocument.Parse(e.WebMessageAsJson);
        var root = message.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var type)) return;

        switch (type.GetString())
        {
            case "ready":
                _isReady = true;
                PushFontSize();
                PushCompact();
                PushHtml();
                break;

            case "toggleTask" when root.TryGetProperty("index", out var indexElement) && indexElement.TryGetInt32(out var index):
                if (ToggleTaskCommand?.CanExecute(index) == true) ToggleTaskCommand.Execute(index);
                break;

            case "openImage" when root.TryGetProperty("src", out var srcElement) && ToAssetPath(srcElement.GetString()) is { } path:
                if (OpenImageCommand?.CanExecute(path) == true) OpenImageCommand.Execute(path);
                break;
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.StartsWith($"https://{PreviewHosts.App}/", StringComparison.OrdinalIgnoreCase)) return;
        e.Cancel = true;
        OpenExternally(e.Uri);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        OpenExternally(e.Uri);
    }

    /// <summary>https://notes.opennotes/{noteId}/assets/x.png → assets/x.png</summary>
    private static string? ToAssetPath(string? src)
    {
        var prefix = $"https://{PreviewHosts.Notes}/";
        if (src is null || !src.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;

        var rest = src[prefix.Length..];
        var slash = rest.IndexOf('/');
        return slash < 0 ? null : Uri.UnescapeDataString(rest[(slash + 1)..]);
    }

    private static void OpenExternally(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)) return;
        if (parsed.Scheme is not ("http" or "https" or "mailto")) return;
        Process.Start(new ProcessStartInfo(parsed.AbsoluteUri) { UseShellExecute = true });
    }

    private void PushHtml() => Execute($"window.openNotes.render({JsonSerializer.Serialize(Html ?? string.Empty)});");

    private void PushFontSize() => Execute($"window.openNotes.setFontSize({ContentFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture)});");

    private void PushCompact() => Execute($"window.openNotes.setCompact({(Compact ? "true" : "false")});");

    private void Execute(string script)
    {
        if (!_isReady || _webView.CoreWebView2 is null) return;
        _ = _webView.CoreWebView2.ExecuteScriptAsync(script);
    }
}
