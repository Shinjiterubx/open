using System.Text;
using System.Text.RegularExpressions;

namespace OpenNotes.Core.Markdown;

/// <summary>Plain-text helpers on Markdown: list snippets, word counts.</summary>
public static partial class MarkdownText
{
    [GeneratedRegex(@"!\[[^\]]*\]\([^)]*\)")]
    private static partial Regex Image();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)")]
    private static partial Regex Link();

    [GeneratedRegex(@"^\s*(?:#{1,6}\s+|>\s?|[-*+]\s+\[[ xX]\]\s+|[-*+]\s+|\d+[.)]\s+)")]
    private static partial Regex BlockMarker();

    [GeneratedRegex(@"[*_`~]{1,3}")]
    private static partial Regex InlineMarker();

    [GeneratedRegex(@"[\p{L}\p{N}][\p{L}\p{N}'’\-]*")]
    private static partial Regex Word();

    /// <summary>A single-line plain-text preview of the content.</summary>
    public static string Snippet(string markdown, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return string.Empty;

        var builder = new StringBuilder();
        var inFence = false;
        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal)) { inFence = !inFence; continue; }
            if (inFence || string.IsNullOrWhiteSpace(line)) continue;
            if (line.Trim() is "---" or "***" or "___") continue;

            line = Image().Replace(line, string.Empty);
            line = Link().Replace(line, "$1");
            line = BlockMarker().Replace(line, string.Empty);
            line = InlineMarker().Replace(line, string.Empty).Trim();
            if (line.Length == 0) continue;

            if (builder.Length > 0) builder.Append(' ');
            builder.Append(line);
            if (builder.Length >= maxLength) break;
        }

        return builder.Length <= maxLength ? builder.ToString() : builder.ToString(0, maxLength).TrimEnd() + "…";
    }

    public static int CountWords(string? text) => string.IsNullOrEmpty(text) ? 0 : Word().Count(text);
}
