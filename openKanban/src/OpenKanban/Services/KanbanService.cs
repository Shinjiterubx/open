using OpenKanban.Core.Abstractions;
using OpenKanban.Core.Boards;
using OpenKanban.Core.Models;

namespace OpenKanban.Services;

/// <summary>
/// Application service: owns the workspaces in memory, applies every change through one place,
/// persists the affected workspace and refreshes the deadline feed. View models only talk to this class.
/// </summary>
public sealed class KanbanService
{
    private readonly IWorkspaceRepository _repository;
    private readonly IWorkspaceSerializer _serializer;
    private readonly IDeadlineFeed _feed;
    private readonly List<Workspace> _workspaces = new();

    public KanbanService(IWorkspaceRepository repository, IWorkspaceSerializer serializer, IDeadlineFeed feed)
    {
        _repository = repository;
        _serializer = serializer;
        _feed = feed;
    }

    /// <summary>Raised after any change (the argument is the affected workspace, or <c>null</c> for structural changes).</summary>
    public event EventHandler<Workspace?>? Changed;

    public IReadOnlyList<Workspace> Workspaces => _workspaces;
    public IReadOnlyList<string> LoadErrors => _repository.LoadErrors;

    public void Load()
    {
        _workspaces.Clear();
        _workspaces.AddRange(_repository.LoadAll());

        if (_workspaces.Count == 0)
        {
            var workspace = new Workspace { Name = "Saját workspace" };
            workspace.Boards.Add(Board.CreateDefault("Első táblám"));
            _workspaces.Add(workspace);
            _repository.Save(workspace);
        }

        _feed.Publish(_workspaces);
        Changed?.Invoke(this, null);
    }

    public Workspace? FindWorkspace(Guid id) => _workspaces.FirstOrDefault(w => w.Id == id);

    public (Workspace Workspace, Board Board)? FindBoard(Guid boardId)
    {
        foreach (var workspace in _workspaces)
        {
            var board = workspace.Boards.FirstOrDefault(b => b.Id == boardId);
            if (board is not null) return (workspace, board);
        }
        return null;
    }

    public (Workspace Workspace, CardLocation Location)? FindCard(Guid cardId) => BoardOperations.FindCard(_workspaces, cardId);

    // ───────────────────────── workspaces ─────────────────────────

    public Workspace AddWorkspace(string name, string color, string? description)
    {
        var workspace = new Workspace { Name = Clean(name, "Workspace"), Color = color, Description = NullIfEmpty(description) };
        workspace.Boards.Add(Board.CreateDefault("Új tábla"));
        _workspaces.Add(workspace);
        Persist(workspace, structural: true);
        return workspace;
    }

    public void UpdateWorkspace(Workspace workspace, string name, string color, string? description)
    {
        workspace.Name = Clean(name, workspace.Name);
        workspace.Color = color;
        workspace.Description = NullIfEmpty(description);
        Persist(workspace, structural: true);
    }

    public void DeleteWorkspace(Workspace workspace)
    {
        _repository.Delete(workspace.Id);
        _workspaces.Remove(workspace);
        _feed.Publish(_workspaces);
        Changed?.Invoke(this, null);
    }

    // ───────────────────────── boards ─────────────────────────

    public Board AddBoard(Workspace workspace, string name)
    {
        var board = Board.CreateDefault(Clean(name, "Új tábla"));
        workspace.Boards.Add(board);
        Persist(workspace, structural: true);
        return board;
    }

    public void UpdateBoard(Workspace workspace, Board board, string name, string background, IReadOnlyList<CardLabel> labels)
    {
        board.Name = Clean(name, board.Name);
        board.Background = background;

        // Remove references to deleted labels from the cards.
        var remaining = labels.Select(l => l.Id).ToHashSet();
        foreach (var card in board.Columns.SelectMany(c => c.Cards))
            card.LabelIds.RemoveAll(id => !remaining.Contains(id));

        board.Labels = labels.ToList();
        Persist(workspace, structural: true);
    }

    public Board DuplicateBoard(Workspace workspace, Board board)
    {
        var temp = new Workspace { Boards = { board } };
        var copy = BoardOperations.CloneWithNewIds(temp).Boards[0];
        copy.Name = board.Name + " (másolat)";
        workspace.Boards.Insert(workspace.Boards.IndexOf(board) + 1, copy);
        Persist(workspace, structural: true);
        return copy;
    }

    public void DeleteBoard(Workspace workspace, Board board)
    {
        workspace.Boards.Remove(board);
        Persist(workspace, structural: true);
    }

    // ───────────────────────── columns ─────────────────────────

    public BoardColumn AddColumn(Workspace workspace, Board board, string name)
    {
        var column = new BoardColumn
        {
            Name = Clean(name, "Új oszlop"),
            Color = BoardColumn.Palette[board.Columns.Count % BoardColumn.Palette.Length],
        };
        board.Columns.Add(column);
        Persist(workspace);
        return column;
    }

    public void UpdateColumn(Workspace workspace, BoardColumn column, string name, string color, int? cardLimit, bool marksDone)
    {
        column.Name = Clean(name, column.Name);
        column.Color = color;
        column.CardLimit = cardLimit is > 0 ? cardLimit : null;
        column.MarksCardsDone = marksDone;
        Persist(workspace);
    }

    public void DeleteColumn(Workspace workspace, Board board, BoardColumn column)
    {
        board.Columns.Remove(column);
        Persist(workspace);
    }

    public bool MoveColumn(Workspace workspace, Board board, Guid columnId, int index)
    {
        if (!BoardOperations.MoveColumn(board, columnId, index)) return false;
        Persist(workspace);
        return true;
    }

    // ───────────────────────── cards ─────────────────────────

    /// <returns><c>null</c> if the column is full (WIP limit).</returns>
    public Card? AddCard(Workspace workspace, BoardColumn column, string title)
    {
        if (!BoardOperations.HasRoom(column)) return null;

        var card = new Card { Title = title.Trim(), IsDone = column.MarksCardsDone };
        column.Cards.Add(card);
        Persist(workspace);
        return card;
    }

    /// <summary>Replaces a card with its edited copy (same id), optionally moving it to another column (appended).</summary>
    public MoveResult SaveCard(Card edited, Guid? targetColumnId = null)
    {
        if (FindCard(edited.Id) is not { } found) return MoveResult.NotFound;
        var (workspace, location) = found;

        edited.UpdatedAt = DateTimeOffset.Now;
        location.Column.Cards[location.Index] = edited;

        var result = MoveResult.Unchanged;
        if (targetColumnId is { } columnId && columnId != location.Column.Id
            && location.Board.Columns.FirstOrDefault(c => c.Id == columnId) is { } target)
        {
            result = BoardOperations.MoveCard(location.Board, edited.Id, target, target.Cards.Count);
        }

        Persist(workspace);
        return result;
    }

    public MoveResult MoveCard(Guid cardId, Guid targetColumnId, int index)
    {
        if (FindCard(cardId) is not { } found) return MoveResult.NotFound;
        var (workspace, location) = found;
        if (location.Board.Columns.FirstOrDefault(c => c.Id == targetColumnId) is not { } target) return MoveResult.NotFound;

        var result = BoardOperations.MoveCard(location.Board, cardId, target, index);
        if (result == MoveResult.Moved) Persist(workspace);
        return result;
    }

    public void SetCardDone(Guid cardId, bool done)
    {
        if (FindCard(cardId) is not { } found || found.Location.Card.IsDone == done) return;
        found.Location.Card.IsDone = done;
        found.Location.Card.UpdatedAt = DateTimeOffset.Now;
        Persist(found.Workspace);
    }

    public void DeleteCard(Guid cardId)
    {
        if (FindCard(cardId) is not { } found) return;
        found.Location.Column.Cards.RemoveAt(found.Location.Index);
        Persist(found.Workspace);
    }

    // ───────────────────────── import / export ─────────────────────────

    public void Export(Workspace workspace, string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        _serializer.Write(workspace, stream);
    }

    public Workspace ReadFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return _serializer.Read(stream);
    }

    /// <param name="replaceExisting">Same id exists: replace it (<c>true</c>) or import as a copy with new ids (<c>false</c>).</param>
    public Workspace Import(Workspace workspace, bool replaceExisting)
    {
        var existing = FindWorkspace(workspace.Id);
        if (existing is not null && replaceExisting)
        {
            _workspaces[_workspaces.IndexOf(existing)] = workspace;
        }
        else
        {
            if (existing is not null)
            {
                workspace = BoardOperations.CloneWithNewIds(workspace);
                workspace.Name += " (importált)";
            }
            _workspaces.Add(workspace);
        }

        Persist(workspace, structural: true);
        return workspace;
    }

    // ───────────────────────── helpers ─────────────────────────

    private void Persist(Workspace workspace, bool structural = false)
    {
        workspace.UpdatedAt = DateTimeOffset.Now;
        _repository.Save(workspace);
        _feed.Publish(_workspaces);
        Changed?.Invoke(this, structural ? null : workspace);
    }

    private static string Clean(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
