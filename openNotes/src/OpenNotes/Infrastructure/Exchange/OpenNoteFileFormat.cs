using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Models;
using OpenNotes.Infrastructure.Storage;

namespace OpenNotes.Infrastructure.Exchange;

/// <summary>Import / export of the native, shareable .openNote file.</summary>
public sealed class OpenNoteFileFormat : INoteImporter, INoteExporter
{
    private readonly INoteSerializer _serializer;

    public OpenNoteFileFormat(INoteSerializer serializer) => _serializer = serializer;

    public string DisplayName => "openNotes jegyzet";
    public IReadOnlyList<string> Extensions { get; } = new[] { OpenNoteSerializer.FileExtension };
    public NoteExportFormat Format => NoteExportFormat.OpenNote;
    public string Extension => OpenNoteSerializer.FileExtension;

    public Note Import(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return _serializer.Read(stream);
    }

    public void Export(Note note, string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        _serializer.Write(note, stream);
    }
}
