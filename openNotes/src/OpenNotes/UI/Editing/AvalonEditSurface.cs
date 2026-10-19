using ICSharpCode.AvalonEdit;
using OpenNotes.Core.Editing;

namespace OpenNotes.UI.Editing;

/// <summary>Adapts AvalonEdit's <see cref="TextEditor"/> to <see cref="ITextSurface"/>.</summary>
internal sealed class AvalonEditSurface : ITextSurface
{
    private readonly TextEditor _editor;

    public AvalonEditSurface(TextEditor editor) => _editor = editor;

    public string Text => _editor.Document.Text;
    public int SelectionStart => _editor.SelectionStart;
    public int SelectionLength => _editor.SelectionLength;
    public int CaretOffset => _editor.CaretOffset;

    public void Replace(int offset, int length, string text) => _editor.Document.Replace(offset, length, text);

    public void Select(int start, int length)
    {
        _editor.Select(start, length);
        _editor.TextArea.Caret.BringCaretToView();
    }

    public IDisposable BeginChange() => _editor.Document.RunUpdate();
}
