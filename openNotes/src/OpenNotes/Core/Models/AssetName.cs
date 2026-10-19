using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenNotes.Core.Models;

/// <summary>Rules for naming note assets safely (no path traversal, URL friendly).</summary>
public static partial class AssetName
{
    private const int MaxStemLength = 60;

    [GeneratedRegex(@"[^A-Za-z0-9_-]+")]
    private static partial Regex InvalidStemChars();

    [GeneratedRegex(@"[^a-z0-9.]+")]
    private static partial Regex InvalidExtensionChars();

    public static string Sanitize(string? fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        var extension = InvalidExtensionChars().Replace(Path.GetExtension(name).ToLowerInvariant(), string.Empty);
        var stem = RemoveDiacritics(Path.GetFileNameWithoutExtension(name));
        stem = InvalidStemChars().Replace(stem, "-").Trim('-');

        if (stem.Length == 0) stem = "image";
        if (stem.Length > MaxStemLength) stem = stem[..MaxStemLength];

        return stem + extension;
    }

    public static string MakeUnique(string name, Func<string, bool> exists)
    {
        if (!exists(name)) return name;

        var stem = Path.GetFileNameWithoutExtension(name);
        var extension = Path.GetExtension(name);
        for (var i = 2; ; i++)
        {
            var candidate = $"{stem}-{i}{extension}";
            if (!exists(candidate)) return candidate;
        }
    }

    public static string ToRelativePath(string name) => $"{Note.AssetFolder}/{name}";

    public static bool IsAssetPath(string url) =>
        url.StartsWith(Note.AssetFolder + "/", StringComparison.OrdinalIgnoreCase);

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
