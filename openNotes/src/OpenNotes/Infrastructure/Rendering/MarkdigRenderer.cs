using Markdig;
using OpenNotes.Core.Abstractions;

namespace OpenNotes.Infrastructure.Rendering;

/// <summary>
/// Markdown → HTML with Markdig (tables, task lists, strikethrough, autolinks...).
/// Raw HTML is disabled so notes received from others cannot inject scripts into the preview.
/// </summary>
public sealed class MarkdigRenderer : IMarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseSoftlineBreakAsHardlineBreak()
        .DisableHtml()
        .Build();

    public string RenderHtml(string markdown, Func<string, string>? linkRewriter = null)
    {
        using var writer = new StringWriter();
        var renderer = new Markdig.Renderers.HtmlRenderer(writer);
        _pipeline.Setup(renderer);
        if (linkRewriter is not null) renderer.LinkRewriter = linkRewriter;

        var document = Markdig.Markdown.Parse(markdown ?? string.Empty, _pipeline);
        renderer.Render(document);
        writer.Flush();
        return writer.ToString();
    }
}
