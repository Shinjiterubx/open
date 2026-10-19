using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace OpenNotes.UI.Editing;

/// <summary>Loads the embedded Markdown highlighting definition once.</summary>
public static class MarkdownHighlighting
{
    private const string ResourceName = "OpenNotes.UI.Editing.MarkdownHighlighting.xshd";

    private static readonly Lazy<IHighlightingDefinition?> LazyDefinition = new(Load);

    public static IHighlightingDefinition? Definition => LazyDefinition.Value;

    private static IHighlightingDefinition? Load()
    {
        using var stream = typeof(MarkdownHighlighting).Assembly.GetManifestResourceStream(ResourceName);
        if (stream is null) return null;

        using var reader = XmlReader.Create(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}
