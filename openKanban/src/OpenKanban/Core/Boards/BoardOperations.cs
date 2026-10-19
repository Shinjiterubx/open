using OpenKanban.Core.Models;

namespace OpenKanban.Core.Boards;

public enum MoveResult
{
    Moved,
    Unchanged,
    ColumnFull,
    NotFound,
}

/// <summary>Location of a card on a board.</summary>
public sealed record CardLocation(Board Board, BoardColumn Column, Card Card, int Index);

/// <summary>Pure board manipulation rules (UI independent, unit tested).</summary>
public static class BoardOperations
{
    public static CardLocation? FindCard(Board board, Guid cardId)
    {
        foreach (var column in board.Columns)
        {
            var index = column.Cards.FindIndex(c => c.Id == cardId);
            if (index >= 0) return new CardLocation(board, column, column.Cards[index], index);
        }
        return null;
    }

    public static (Workspace Workspace, CardLocation Location)? FindCard(IEnumerable<Workspace> workspaces, Guid cardId)
    {
        foreach (var workspace in workspaces)
        foreach (var board in workspace.Boards)
        {
            if (FindCard(board, cardId) is { } location) return (workspace, location);
        }
        return null;
    }

    /// <summary>Can one more card be added to the column (WIP limit)?</summary>
    public static bool HasRoom(BoardColumn column) => !column.IsFull;

    /// <summary>
    /// Moves a card to <paramref name="target"/> at <paramref name="index"/> (index in the target's list
    /// as seen before the move; clamped). Moving into a full column is refused (WIP limit), reordering inside
    /// the same column is always allowed. Columns flagged <see cref="BoardColumn.MarksCardsDone"/> mark the card done.
    /// </summary>
    public static MoveResult MoveCard(Board board, Guid cardId, BoardColumn target, int index)
    {
        if (FindCard(board, cardId) is not { } from) return MoveResult.NotFound;
        if (!board.Columns.Contains(target)) return MoveResult.NotFound;

        var sameColumn = from.Column == target;
        if (!sameColumn && target.IsFull) return MoveResult.ColumnFull;

        index = Math.Clamp(index, 0, target.Cards.Count);
        if (sameColumn)
        {
            // Removing first shifts later positions by one.
            if (index > from.Index) index--;
            if (index == from.Index) return MoveResult.Unchanged;
        }

        from.Column.Cards.RemoveAt(from.Index);
        target.Cards.Insert(Math.Clamp(index, 0, target.Cards.Count), from.Card);

        if (!sameColumn)
        {
            if (target.MarksCardsDone) from.Card.IsDone = true;
            else if (from.Column.MarksCardsDone) from.Card.IsDone = false;
        }

        from.Card.UpdatedAt = DateTimeOffset.Now;
        return MoveResult.Moved;
    }

    /// <summary>Moves a column to a new position (index as seen before the move).</summary>
    public static bool MoveColumn(Board board, Guid columnId, int index)
    {
        var from = board.Columns.FindIndex(c => c.Id == columnId);
        if (from < 0) return false;

        index = Math.Clamp(index, 0, board.Columns.Count);
        if (index > from) index--;
        if (index == from) return false;

        var column = board.Columns[from];
        board.Columns.RemoveAt(from);
        board.Columns.Insert(index, column);
        return true;
    }

    /// <summary>Deep copy with fresh ids (duplicate / import as copy). Label references are remapped.</summary>
    public static Workspace CloneWithNewIds(Workspace source)
    {
        var copy = new Workspace
        {
            Name = source.Name,
            Description = source.Description,
            Color = source.Color,
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now,
        };

        foreach (var board in source.Boards)
        {
            var labelMap = board.Labels.ToDictionary(l => l.Id, _ => Guid.NewGuid());
            copy.Boards.Add(new Board
            {
                Name = board.Name,
                Background = board.Background,
                CreatedAt = board.CreatedAt,
                Labels = board.Labels.Select(l => new CardLabel { Id = labelMap[l.Id], Name = l.Name, Color = l.Color }).ToList(),
                Columns = board.Columns.Select(c => new BoardColumn
                {
                    Name = c.Name,
                    Color = c.Color,
                    CardLimit = c.CardLimit,
                    MarksCardsDone = c.MarksCardsDone,
                    Cards = c.Cards.Select(card =>
                    {
                        var clone = card.Clone();
                        clone.Id = Guid.NewGuid();
                        clone.LabelIds = card.LabelIds.Where(labelMap.ContainsKey).Select(id => labelMap[id]).ToList();
                        foreach (var item in clone.Checklist) item.Id = Guid.NewGuid();
                        return clone;
                    }).ToList(),
                }).ToList(),
            });
        }

        return copy;
    }
}
