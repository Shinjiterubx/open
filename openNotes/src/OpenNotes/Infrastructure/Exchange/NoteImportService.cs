using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Models;

namespace OpenNotes.Infrastructure.Exchange;

/// <summary>Picks the right <see cref="INoteImporter"/> for a file (open/closed: add formats by registering importers).</summary>
public sealed class NoteImportService
{
    private readonly IReadOnlyList<INoteImporter> _importers;

    public NoteImportService(IEnumerable<INoteImporter> importers) => _importers = importers.ToList();

    public bool CanImport(string filePath) => FindImporter(filePath) is not null;

    public Note Import(string filePath) =>
        (FindImporter(filePath) ?? throw new NotSupportedException($"Nem támogatott fájltípus: {Path.GetExtension(filePath)}"))
        .Import(filePath);

    /// <summary>File dialog filter, e.g. "Minden támogatott|*.openNote;*.md|openNotes jegyzet|*.openNote|..."</summary>
    public string DialogFilter
    {
        get
        {
            var all = string.Join(";", _importers.SelectMany(i => i.Extensions).Select(e => "*" + e));
            var each = _importers.Select(i => $"{i.DisplayName} ({string.Join(", ", i.Extensions.Select(e => "*" + e))})|{string.Join(";", i.Extensions.Select(e => "*" + e))}");
            return string.Join("|", new[] { $"Minden támogatott fájl|{all}" }.Concat(each));
        }
    }

    private INoteImporter? FindImporter(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return _importers.FirstOrDefault(i => i.Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase));
    }
}
