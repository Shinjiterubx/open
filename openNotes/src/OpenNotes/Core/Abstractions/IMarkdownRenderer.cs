namespace OpenNotes.Core.Abstractions;

/// <summary>Turns Markdown into HTML for the preview.</summary>
public interface IMarkdownRenderer
{
    /// <param name="markdown">Source text.</param>
    /// <param name="linkRewriter">Optional rewrite of link / image URLs (e.g. to resolve note assets).</param>
    string RenderHtml(string markdown, Func<string, string>? linkRewriter = null);
}
