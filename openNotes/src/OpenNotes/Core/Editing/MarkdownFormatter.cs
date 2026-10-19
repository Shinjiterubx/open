using System.Text.RegularExpressions;

namespace OpenNotes.Core.Editing;

public enum BlockStyle
{
    Paragraph,
    Heading1,
    Heading2,
    Heading3,
    BulletList,
    NumberedList,
    TaskList,
}

/// <summary>
/// Markdown editing commands (bold, headings, lists, task toggling, list continuation...).
/// Pure logic on top of <see cref="ITextSurface"/>, so it works with any editor control.
/// </summary>
public sealed partial class MarkdownFormatter
{
    public const string TableTemplate =
        "| Oszlop 1 | Oszlop 2 | Oszlop 3 |\n" +
        "| --- | --- | --- |\n" +
        "|  |  |  |";

    [GeneratedRegex(@"^(?<indent>[ \t]*)(?<marker>#{1,6}[ \t]+|[-*+][ \t]+\[[ xX]\][ \t]+|[-*+][ \t]+|\d+[.)][ \t]+)?")]
    private static partial Regex BlockMarker();

    [GeneratedRegex(@"^(?<indent>[ \t]*)(?:(?<task>(?<taskBullet>[-*+])[ \t]+\[[ xX]\][ \t]+)|(?<bullet>[-*+][ \t]+)|(?<num>\d+)(?<delim>[.)])[ \t]+|(?<quote>>[ \t]?))(?<rest>.*)$")]
    private static partial Regex ListLine();

    [GeneratedRegex(@"^[ \t]*(?:>[ \t]?)*[ \t]*(?:[-*+]|\d+[.)])[ \t]+\[(?<state>[ xX])\]")]
    private static partial Regex TaskBox();

    [GeneratedRegex(@"^[ \t]*>[ \t]?")]
    private static partial Regex QuoteMarker();

    [GeneratedRegex(@"^(https?://|mailto:|www\.)\S+$", RegexOptions.IgnoreCase)]
    private static partial Regex UrlLike();

    // ───────────────────────── inline ─────────────────────────

    /// <summary>Wraps the selection in <paramref name="prefix"/>/<paramref name="suffix"/>, or unwraps it if already wrapped.</summary>
    public void ToggleWrap(ITextSurface surface, string prefix, string suffix, string placeholder)
    {
        var text = surface.Text;
        var start = surface.SelectionStart;
        var length = surface.SelectionLength;

        using var _ = surface.BeginChange();

        if (length == 0)
        {
            surface.Replace(start, 0, prefix + placeholder + suffix);
            surface.Select(start + prefix.Length, placeholder.Length);
            return;
        }

        var wrappedOutside =
            start >= prefix.Length &&
            start + length + suffix.Length <= text.Length &&
            string.CompareOrdinal(text, start - prefix.Length, prefix, 0, prefix.Length) == 0 &&
            string.CompareOrdinal(text, start + length, suffix, 0, suffix.Length) == 0;

        if (wrappedOutside)
        {
            surface.Replace(start + length, suffix.Length, string.Empty);
            surface.Replace(start - prefix.Length, prefix.Length, string.Empty);
            surface.Select(start - prefix.Length, length);
            return;
        }

        var selected = text.Substring(start, length);
        if (selected.Length >= prefix.Length + suffix.Length &&
            selected.StartsWith(prefix, StringComparison.Ordinal) &&
            selected.EndsWith(suffix, StringComparison.Ordinal))
        {
            var inner = selected.Substring(prefix.Length, selected.Length - prefix.Length - suffix.Length);
            surface.Replace(start, length, inner);
            surface.Select(start, inner.Length);
            return;
        }

        // Keep surrounding whitespace outside the markers: "**word** " instead of "**word **".
        var lead = selected.Length - selected.TrimStart().Length;
        var core = selected.Trim();
        if (core.Length == 0) core = placeholder;
        surface.Replace(start + lead, selected.Trim().Length, prefix + core + suffix);
        surface.Select(start + lead + prefix.Length, core.Length);
    }

    public void InsertLink(ITextSurface surface)
    {
        var start = surface.SelectionStart;
        var selected = surface.Text.Substring(start, surface.SelectionLength).Trim();

        using var _ = surface.BeginChange();

        if (selected.Length > 0 && UrlLike().IsMatch(selected))
        {
            const string label = "link szövege";
            surface.Replace(start, surface.SelectionLength, $"[{label}]({selected})");
            surface.Select(start + 1, label.Length);
        }
        else if (selected.Length > 0)
        {
            const string url = "https://";
            surface.Replace(start, surface.SelectionLength, $"[{selected}]({url})");
            surface.Select(start + selected.Length + 3, url.Length);
        }
        else
        {
            const string label = "link szövege";
            surface.Replace(start, 0, $"[{label}](https://)");
            surface.Select(start + 1, label.Length);
        }
    }

    // ───────────────────────── blocks ─────────────────────────

    /// <summary>Applies a block style to every selected line; applying the same style again removes it.</summary>
    public void ToggleBlock(ITextSurface surface, BlockStyle style)
    {
        var text = surface.Text;
        var lines = GetSelectedLines(text, surface.SelectionStart, surface.SelectionStart + surface.SelectionLength);
        var content = lines.Where(l => lines.Count == 1 || !string.IsNullOrWhiteSpace(text.Substring(l.Start, l.Length))).ToList();
        var remove = style == BlockStyle.Paragraph || content.All(l => HasStyle(text.Substring(l.Start, l.Length), style));

        // Build replacements top-down (numbering), apply bottom-up (offsets stay valid).
        var replacements = new List<(int Start, int Length, string NewText)>();
        var number = 1;
        foreach (var line in content)
        {
            var lineText = text.Substring(line.Start, line.Length);
            var match = BlockMarker().Match(lineText);
            var indent = match.Groups["indent"].Value;
            var body = lineText[(indent.Length + match.Groups["marker"].Length)..];
            var marker = remove ? string.Empty : MarkerFor(style, number++);
            replacements.Add((line.Start, line.Length, indent + marker + body));
        }

        ApplyLineReplacements(surface, replacements, selectWhole: lines.Count > 1);
    }

    public void ToggleQuote(ITextSurface surface)
    {
        var text = surface.Text;
        var lines = GetSelectedLines(text, surface.SelectionStart, surface.SelectionStart + surface.SelectionLength);
        var remove = lines.All(l => QuoteMarker().IsMatch(text.Substring(l.Start, l.Length)));

        var replacements = lines.Select(l =>
        {
            var lineText = text.Substring(l.Start, l.Length);
            var newText = remove ? QuoteMarker().Replace(lineText, string.Empty, 1) : "> " + lineText;
            return (l.Start, l.Length, newText);
        }).ToList();

        ApplyLineReplacements(surface, replacements, selectWhole: lines.Count > 1);
    }

    /// <summary>Ctrl+Enter: checks / unchecks the task on the caret line, or turns the line into a task.</summary>
    public void ToggleTaskAtCaret(ITextSurface surface)
    {
        var text = surface.Text;
        var caret = surface.CaretOffset;
        var lineStart = LineStart(text, caret);
        var line = text.Substring(lineStart, LineEnd(text, caret) - lineStart);

        var match = TaskBox().Match(line);
        if (!match.Success)
        {
            ToggleBlock(surface, BlockStyle.TaskList);
            return;
        }

        var stateOffset = lineStart + match.Groups["state"].Index;
        var newState = text[stateOffset] == ' ' ? "x" : " ";
        using var _ = surface.BeginChange();
        surface.Replace(stateOffset, 1, newState);
        surface.Select(caret, 0);
    }

    public void InsertCodeBlock(ITextSurface surface)
    {
        var text = surface.Text;
        var start = surface.SelectionStart;
        var length = surface.SelectionLength;
        var newLine = DetectNewLine(text);

        using var _ = surface.BeginChange();

        if (length > 0)
        {
            var lineStart = LineStart(text, start);
            var lineEnd = LineEnd(text, start + length);
            var body = text.Substring(lineStart, lineEnd - lineStart);
            var block = "```" + newLine + body + newLine + "```";
            surface.Replace(lineStart, lineEnd - lineStart, block);
            surface.Select(lineStart + 3, 0);
            return;
        }

        var (before, after) = BlockPadding(text, start, newLine);
        surface.Replace(start, 0, before + "```" + newLine + newLine + "```" + after);
        surface.Select(start + before.Length + 3 + newLine.Length, 0);
    }

    /// <summary>Inserts a block (table, rule, image...) on its own line(s) at the caret.</summary>
    public void InsertBlock(ITextSurface surface, string block)
    {
        var text = surface.Text;
        var newLine = DetectNewLine(text);
        block = block.Replace("\r\n", "\n").Replace("\n", newLine);

        var start = surface.SelectionStart;
        var (before, after) = BlockPadding(text, start + surface.SelectionLength, newLine);

        using var _ = surface.BeginChange();
        surface.Replace(start, surface.SelectionLength, before + block + after);
        surface.Select(start + before.Length + block.Length + after.Length, 0);
    }

    public void InsertImage(ITextSurface surface, string altText, string relativePath) =>
        InsertBlock(surface, $"![{altText}]({relativePath})");

    /// <summary>
    /// Enter inside a list / quote continues it with the next marker; Enter on an empty item ends the list.
    /// Returns <c>false</c> when the default Enter behaviour should run.
    /// </summary>
    public bool TryContinueList(ITextSurface surface)
    {
        if (surface.SelectionLength > 0) return false;

        var text = surface.Text;
        var caret = surface.CaretOffset;
        var lineStart = LineStart(text, caret);
        var lineEnd = LineEnd(text, caret);
        var line = text.Substring(lineStart, lineEnd - lineStart);

        var match = ListLine().Match(line);
        if (!match.Success) return false;

        var contentStart = lineStart + line.Length - match.Groups["rest"].Length;
        if (caret < contentStart) return false;

        using var _ = surface.BeginChange();

        if (string.IsNullOrWhiteSpace(match.Groups["rest"].Value))
        {
            // Empty item → leave the list.
            surface.Replace(lineStart, lineEnd - lineStart, string.Empty);
            surface.Select(lineStart, 0);
            return true;
        }

        var indent = match.Groups["indent"].Value;
        string marker;
        if (match.Groups["task"].Success) marker = match.Groups["taskBullet"].Value + " [ ] ";
        else if (match.Groups["bullet"].Success) marker = match.Groups["bullet"].Value;
        else if (match.Groups["num"].Success)
            marker = (int.TryParse(match.Groups["num"].Value, out var n) ? n + 1 : 1) + match.Groups["delim"].Value + " ";
        else marker = "> ";

        var insert = DetectNewLine(text) + indent + marker;
        surface.Replace(caret, 0, insert);
        surface.Select(caret + insert.Length, 0);
        return true;
    }

    // ───────────────────────── helpers ─────────────────────────

    private static void ApplyLineReplacements(ITextSurface surface, List<(int Start, int Length, string NewText)> replacements, bool selectWhole)
    {
        if (replacements.Count == 0) return;

        using var _ = surface.BeginChange();

        var delta = 0;
        for (var i = replacements.Count - 1; i >= 0; i--)
        {
            var (start, length, newText) = replacements[i];
            surface.Replace(start, length, newText);
            delta += newText.Length - length;
        }

        var first = replacements[0];
        var last = replacements[^1];
        var newEnd = last.Start + last.Length + delta;

        if (selectWhole) surface.Select(first.Start, newEnd - first.Start);
        else surface.Select(first.Start + first.NewText.Length, 0);
    }

    private static bool HasStyle(string line, BlockStyle style)
    {
        var marker = BlockMarker().Match(line).Groups["marker"].Value.TrimEnd();
        return style switch
        {
            BlockStyle.Heading1 => marker == "#",
            BlockStyle.Heading2 => marker == "##",
            BlockStyle.Heading3 => marker == "###",
            BlockStyle.TaskList => marker.Length > 0 && marker.EndsWith(']'),
            BlockStyle.BulletList => marker.Length == 1 && marker[0] is '-' or '*' or '+',
            BlockStyle.NumberedList => marker.Length > 1 && char.IsDigit(marker[0]),
            _ => marker.Length == 0,
        };
    }

    private static string MarkerFor(BlockStyle style, int number) => style switch
    {
        BlockStyle.Heading1 => "# ",
        BlockStyle.Heading2 => "## ",
        BlockStyle.Heading3 => "### ",
        BlockStyle.BulletList => "- ",
        BlockStyle.NumberedList => $"{number}. ",
        BlockStyle.TaskList => "- [ ] ",
        _ => string.Empty,
    };

    /// <summary>Newlines needed so a block sits on its own line with a blank line above it.</summary>
    private static (string Before, string After) BlockPadding(string text, int offset, string newLine)
    {
        var lineStart = LineStart(text, offset);
        string before;
        if (offset == 0) before = string.Empty;
        else if (offset > lineStart) before = newLine + newLine;           // text before caret on this line
        else if (lineStart >= 2 && !IsBlankLineBefore(text, lineStart)) before = newLine;
        else before = string.Empty;

        var lineEnd = LineEnd(text, offset);
        // Text after the caret moves below the block; otherwise one newline keeps a blank line before the next block.
        var after = lineEnd > offset ? newLine + newLine : newLine;
        return (before, after);
    }

    private static bool IsBlankLineBefore(string text, int lineStart)
    {
        var previousStart = LineStart(text, lineStart - 1);
        return string.IsNullOrWhiteSpace(text.Substring(previousStart, lineStart - previousStart));
    }

    internal static List<(int Start, int Length)> GetSelectedLines(string text, int start, int end)
    {
        var lines = new List<(int Start, int Length)>();
        // A selection ending right after a newline does not include the next line.
        var effectiveEnd = end > start && end > 0 && text[end - 1] == '\n' ? end - 1 : end;

        var lineStart = LineStart(text, start);
        while (true)
        {
            var lineEnd = LineEnd(text, lineStart);
            lines.Add((lineStart, lineEnd - lineStart));

            var newLine = text.IndexOf('\n', lineStart);
            if (newLine < 0) break;
            var next = newLine + 1;
            if (next > effectiveEnd) break;
            lineStart = next;
        }

        return lines;
    }

    internal static int LineStart(string text, int offset)
    {
        if (offset <= 0) return 0;
        var index = text.LastIndexOf('\n', Math.Min(offset, text.Length) - 1);
        return index + 1;
    }

    internal static int LineEnd(string text, int offset)
    {
        var index = text.IndexOf('\n', Math.Min(offset, text.Length));
        if (index < 0) return text.Length;
        return index > 0 && text[index - 1] == '\r' ? index - 1 : index;
    }

    internal static string DetectNewLine(string text) =>
        text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n"
        : text.Contains('\n') ? "\n"
        : Environment.NewLine;
}
