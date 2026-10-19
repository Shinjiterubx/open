using OpenNotes.Core.Editing;

namespace OpenNotes.Tests;

/// <summary>In-memory <see cref="ITextSurface"/> for formatter tests. Use "|" for the caret and "[" "]" for a selection.</summary>
internal sealed class FakeTextSurface : ITextSurface
{
    public FakeTextSurface(string markup)
    {
        var caret = markup.IndexOf('|');
        if (caret >= 0)
        {
            Text = markup.Remove(caret, 1);
            SelectionStart = caret;
            return;
        }

        var start = markup.IndexOf('[');
        var end = markup.IndexOf(']', start + 1);
        Text = markup.Remove(end, 1).Remove(start, 1);
        SelectionStart = start;
        SelectionLength = end - start - 1;
    }

    public string Text { get; private set; }

    /// <summary>Text with "\n" line endings (the formatter may insert Environment.NewLine).</summary>
    public string Normalized => Text.Replace("\r\n", "\n");
    public int SelectionStart { get; private set; }
    public int SelectionLength { get; private set; }
    public int CaretOffset => SelectionStart + SelectionLength;

    public void Replace(int offset, int length, string text) => Text = Text.Remove(offset, length).Insert(offset, text);

    public void Select(int start, int length)
    {
        SelectionStart = start;
        SelectionLength = length;
    }

    public IDisposable BeginChange() => new Noop();

    private sealed class Noop : IDisposable
    {
        public void Dispose() { }
    }
}
