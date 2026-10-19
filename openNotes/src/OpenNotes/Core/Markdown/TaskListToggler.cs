using System.Text.RegularExpressions;

namespace OpenNotes.Core.Markdown;

/// <summary>
/// Flips the n-th task list checkbox (<c>- [ ]</c> / <c>- [x]</c>) in a Markdown document.
/// The index follows document order, exactly as the preview renders checkboxes.
/// </summary>
public static partial class TaskListToggler
{
    [GeneratedRegex(@"^[ \t]*(?:>[ \t]?)*[ \t]*(?:[-*+]|\d+[.)])[ \t]+\[(?<state>[ xX])\](?=\s|$)")]
    private static partial Regex TaskItem();

    public static string Toggle(string markdown, int index)
    {
        if (string.IsNullOrEmpty(markdown) || index < 0) return markdown;

        var position = 0;
        var taskCount = 0;
        string? openFence = null;

        while (position <= markdown.Length)
        {
            var newLine = markdown.IndexOf('\n', position);
            var end = newLine < 0 ? markdown.Length : newLine;
            var line = markdown.Substring(position, end - position);
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                var fence = trimmed[..3];
                if (openFence is null) openFence = fence;
                else if (openFence == fence) openFence = null;
            }
            else if (openFence is null)
            {
                var match = TaskItem().Match(line);
                if (match.Success)
                {
                    if (taskCount == index)
                    {
                        var stateIndex = position + match.Groups["state"].Index;
                        var newState = markdown[stateIndex] == ' ' ? 'x' : ' ';
                        return string.Concat(markdown.AsSpan(0, stateIndex), newState.ToString(), markdown.AsSpan(stateIndex + 1));
                    }
                    taskCount++;
                }
            }

            if (newLine < 0) break;
            position = newLine + 1;
        }

        return markdown;
    }
}
