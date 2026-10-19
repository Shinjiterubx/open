using OpenKanban.Core.Abstractions;
using OpenKanban.Core.Models;

namespace OpenKanban.Infrastructure.Storage;

/// <summary>
/// One <c>{id}.openKanban</c> file per workspace (same format as export). Atomic writes;
/// deleted workspaces go to the Trash folder.
/// </summary>
public sealed class FileWorkspaceRepository : IWorkspaceRepository
{
    private readonly IAppPaths _paths;
    private readonly IWorkspaceSerializer _serializer;
    private readonly List<string> _loadErrors = new();

    public FileWorkspaceRepository(IAppPaths paths, IWorkspaceSerializer serializer)
    {
        _paths = paths;
        _serializer = serializer;
    }

    public IReadOnlyList<string> LoadErrors => _loadErrors;

    public IReadOnlyList<Workspace> LoadAll()
    {
        _loadErrors.Clear();
        var workspaces = new List<Workspace>();

        foreach (var file in Directory.EnumerateFiles(_paths.WorkspacesDirectory, "*" + OpenKanbanSerializer.FileExtension))
        {
            try
            {
                using var stream = File.OpenRead(file);
                var workspace = _serializer.Read(stream);
                if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var idFromName)) workspace.Id = idFromName;
                workspaces.Add(workspace);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                _loadErrors.Add($"{Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return workspaces.OrderBy(w => w.CreatedAt).ToList();
    }

    public void Save(Workspace workspace)
    {
        var target = PathFor(workspace.Id);
        var temp = target + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            _serializer.Write(workspace, stream);
        }
        File.Move(temp, target, overwrite: true);
    }

    public void Delete(Guid id)
    {
        var source = PathFor(id);
        if (!File.Exists(source)) return;
        var trashName = $"{id:N}_{DateTime.Now:yyyyMMdd-HHmmss}{OpenKanbanSerializer.FileExtension}";
        File.Move(source, Path.Combine(_paths.TrashDirectory, trashName), overwrite: true);
    }

    private string PathFor(Guid id) =>
        Path.Combine(_paths.WorkspacesDirectory, id.ToString("N") + OpenKanbanSerializer.FileExtension);
}
