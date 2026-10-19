namespace OpenNotes.Core.Editing;

/// <summary>
/// Minimal abstraction over a text editor so formatting logic stays UI independent and testable.
/// </summary>
public interface ITextSurface
{
    string Text { get; }
    int SelectionStart { get; }
    int SelectionLength { get; }
    int CaretOffset { get; }

    void Replace(int offset, int length, string text);

    /// <summary>Selects a range; a zero length just places the caret.</summary>
    void Select(int start, int length);

    /// <summary>Groups the following edits into one undo step.</summary>
    IDisposable BeginChange();
}
