using System.IO;
using System.Windows.Media.Imaging;

namespace OpenNotes.UI.Editing;

internal static class ImageFiles
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".svg",
    };

    public const string DialogFilter = "Képek|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp;*.svg|Minden fájl|*.*";

    public static bool IsImage(string path) => Extensions.Contains(Path.GetExtension(path));

    public static byte[] EncodePng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
