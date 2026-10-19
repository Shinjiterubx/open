using System.Text.RegularExpressions;

namespace OpenNotes.Core.Markdown;

/// <summary>Finds and rewrites image references (<c>![alt](path)</c>) in Markdown.</summary>
public static partial class MarkdownImageReferences
{
    [GeneratedRegex(@"!\[(?<alt>[^\]]*)\]\((?<url>[^)\s]+)(?<title>\s+""[^""]*"")?\)")]
    private static partial Regex ImagePattern();

    /// <summary>Replaces image URLs using <paramref name="rewrite"/>; returning the same URL leaves it untouched.</summary>
    public static string RewriteUrls(string markdown, Func<string, string> rewrite) =>
        ImagePattern().Replace(markdown, match =>
        {
            var url = match.Groups["url"].Value;
            var newUrl = rewrite(url);
            return newUrl == url
                ? match.Value
                : $"![{match.Groups["alt"].Value}]({newUrl}{match.Groups["title"].Value})";
        });
}
