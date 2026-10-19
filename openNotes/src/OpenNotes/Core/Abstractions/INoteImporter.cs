using OpenNotes.Core.Models;

namespace OpenNotes.Core.Abstractions;

/// <summary>Creates a note from an external file. One implementation per file format.</summary>
public interface INoteImporter
{
    /// <summary>Human readable format name for file dialogs.</summary>
    string DisplayName { get; }

    /// <summary>File extensions with leading dot, e.g. ".md".</summary>
    IReadOnlyList<string> Extensions { get; }

    Note Import(string filePath);
}
