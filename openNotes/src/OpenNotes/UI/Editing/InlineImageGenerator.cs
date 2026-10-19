using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit.Rendering;
using OpenNotes.Core.Abstractions;

namespace OpenNotes.UI.Editing;

/// <summary>
/// Shows embedded images (<c>![alt](assets/x.png)</c>) as pictures directly in the source editor.
/// On the line holding the caret the raw Markdown is shown instead, so it stays editable.
/// Clicking a picture calls <see cref="ImageClicked"/>.
/// </summary>
internal sealed partial class InlineImageGenerator : VisualLineElementGenerator
{
    [GeneratedRegex(@"!\[[^\]\r\n]*\]\((?<url>assets/[^)\s]+)(?:[ \t]+""[^""\r\n]*"")?\)")]
    private static partial Regex ImagePattern();

    private const double MaxDecodeWidth = 1600;

    private readonly ConditionalWeakTable<byte[], BitmapSource> _bitmaps = new();

    public IAssetSource? Source { get; set; }

    public double MaxWidth { get; set; } = 600;

    public double MaxHeight { get; set; } = 360;

    /// <summary>Line number shown as raw Markdown (the caret line while editing); 0 = none.</summary>
    public int RawLineNumber { get; set; }

    public event Action<string>? ImageClicked;

    /// <summary>Cheap check used to decide whether a line needs redrawing.</summary>
    public static bool ContainsImage(string lineText) =>
        lineText.Contains("](assets/", StringComparison.OrdinalIgnoreCase);

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var match = FindMatch(startOffset);
        return match is null ? -1 : startOffset + match.Value.RelativeIndex;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        var match = FindMatch(offset);
        if (match is null || match.Value.RelativeIndex != 0) return null;

        var found = match.Value.Match;
        var url = found.Groups["url"].Value;
        if (Source?.GetAsset(url) is not { } data || GetBitmap(data) is not { } bitmap) return null;

        return new InlineObjectElement(found.Length, CreateVisual(bitmap, url, found.Value));
    }

    private (Match Match, int RelativeIndex)? FindMatch(int startOffset)
    {
        if (Source is null) return null;

        var visualLine = CurrentContext.VisualLine;
        if (visualLine.FirstDocumentLine.LineNumber == RawLineNumber) return null;

        var endOffset = visualLine.LastDocumentLine.EndOffset;
        if (endOffset <= startOffset) return null;

        var segment = CurrentContext.GetText(startOffset, endOffset - startOffset);
        var match = ImagePattern().Match(segment.Text, segment.Offset, segment.Count);
        return match.Success ? (match, match.Index - segment.Offset) : null;
    }

    private BitmapSource? GetBitmap(byte[] data)
    {
        if (_bitmaps.TryGetValue(data, out var cached)) return cached;

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bitmap.StreamSource = new MemoryStream(data, writable: false);
            bitmap.EndInit();

            BitmapSource result = bitmap;
            if (bitmap.PixelWidth > MaxDecodeWidth)
            {
                // Keep memory low: the editor never shows more than a thumbnail-sized version.
                var scale = MaxDecodeWidth / bitmap.PixelWidth;
                result = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
            }

            result.Freeze();
            _bitmaps.AddOrUpdate(data, result);
            return result;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException or IOException)
        {
            return null;   // e.g. SVG – the raw Markdown stays visible
        }
    }

    private UIElement CreateVisual(BitmapSource bitmap, string url, string markdown)
    {
        var (width, height) = Fit(bitmap.PixelWidth, bitmap.PixelHeight);

        var image = new Image
        {
            Source = bitmap,
            Width = width,
            Height = height,
            Stretch = Stretch.Uniform,
            Clip = new RectangleGeometry(new Rect(0, 0, width, height), 8, 8),
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        var frame = new Border
        {
            Child = image,
            Margin = new Thickness(0, 6, 0, 6),
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent,
            ToolTip = "Kattints a nagyításhoz\n" + markdown,
        };
        frame.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;   // don't move the caret / start a selection
            ImageClicked?.Invoke(url);
        };
        return frame;
    }

    private (double Width, double Height) Fit(double width, double height)
    {
        if (width <= 0 || height <= 0) return (1, 1);
        var scale = Math.Min(1, Math.Min(MaxWidth / width, MaxHeight / height));
        return (Math.Max(1, Math.Round(width * scale)), Math.Max(1, Math.Round(height * scale)));
    }
}
