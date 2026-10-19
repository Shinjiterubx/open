using OpenNotes.Core.Models;

namespace OpenNotes.Core.Abstractions;

/// <summary>Writes a note to an external file. One implementation per file format.</summary>
public interface INoteExporter
{
    NoteExportFormat Format { get; }

    string DisplayName { get; }

    /// <summary>Extension with leading dot, e.g. ".md".</summary>
    string Extension { get; }

    void Export(Note note, string filePath);
}

public enum NoteExportFormat
{
    OpenNote,
    Markdown,
}
