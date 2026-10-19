using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenKanban.Core.Abstractions;
using OpenKanban.Core.Models;

namespace OpenKanban.Infrastructure.Storage;

/// <summary>
/// The <c>.openKanban</c> format is a ZIP archive (see docs/FORMAT.md):
/// <code>
/// manifest.json    – format, version, generator, workspace id / name, counts
/// workspace.json   – the whole workspace: boards → columns → cards
/// </code>
/// </summary>
public sealed class OpenKanbanSerializer : IWorkspaceSerializer
{
    public const string FileExtension = ".openKanban";
    public const string FormatName = "openKanban";
    public const int CurrentVersion = 1;

    private const string ManifestEntry = "manifest.json";
    private const string WorkspaceEntry = "workspace.json";
    private const long MaxEntrySize = 200L * 1024 * 1024;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public void Write(Workspace workspace, Stream destination)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true, Utf8NoBom);

        var manifest = new Manifest(FormatName, CurrentVersion, $"openKanban {AppInfo.Version}", DateTimeOffset.Now,
            workspace.Id, workspace.Name, workspace.Boards.Count, workspace.Boards.Sum(b => b.Columns.Sum(c => c.Cards.Count)));
        WriteJson(archive, ManifestEntry, manifest);
        WriteJson(archive, WorkspaceEntry, workspace);
    }

    public Workspace Read(Stream source)
    {
        using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true, Utf8NoBom);

        var manifest = ReadJson<Manifest>(archive.GetEntry(ManifestEntry)
            ?? throw new InvalidDataException("A fájl nem érvényes openKanban workspace (hiányzik a manifest.json)."));
        if (!string.Equals(manifest.Format, FormatName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Ismeretlen fájlformátum.");
        if (manifest.Version > CurrentVersion)
            throw new InvalidDataException($"A fájl egy újabb openKanban verzióval készült (formátum v{manifest.Version}).");

        var workspace = ReadJson<Workspace>(archive.GetEntry(WorkspaceEntry)
            ?? throw new InvalidDataException("Sérült fájl: hiányzik a workspace.json."));
        Normalize(workspace);
        return workspace;
    }

    /// <summary>Guards against incomplete data from hand edits or other tools.</summary>
    private static void Normalize(Workspace workspace)
    {
        if (workspace.Id == Guid.Empty) workspace.Id = Guid.NewGuid();
        workspace.Boards ??= new List<Board>();
        foreach (var board in workspace.Boards)
        {
            if (board.Id == Guid.Empty) board.Id = Guid.NewGuid();
            board.Columns ??= new List<BoardColumn>();
            board.Labels ??= new List<CardLabel>();
            foreach (var column in board.Columns)
            {
                if (column.Id == Guid.Empty) column.Id = Guid.NewGuid();
                column.Cards ??= new List<Card>();
                if (column.CardLimit is < 1) column.CardLimit = null;
                foreach (var card in column.Cards)
                {
                    if (card.Id == Guid.Empty) card.Id = Guid.NewGuid();
                    card.LabelIds ??= new List<Guid>();
                    card.Checklist ??= new List<ChecklistItem>();
                }
            }
        }
    }

    private static void WriteJson<T>(ZipArchive archive, string name, T value)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, value, JsonOptions);
    }

    private static T ReadJson<T>(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxEntrySize) throw new InvalidDataException($"Túl nagy bejegyzés: {entry.Name}");
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<T>(stream, JsonOptions) ?? throw new InvalidDataException($"Üres bejegyzés: {entry.Name}");
    }

    private sealed record Manifest(
        string Format, int Version, string? Generator, DateTimeOffset ExportedAt,
        Guid WorkspaceId, string? WorkspaceName, int BoardCount, int CardCount);
}
