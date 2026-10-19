using OpenKanban.Core.Boards;
using OpenKanban.Core.Models;
using OpenKanban.Infrastructure.Storage;
using Xunit;

namespace OpenKanban.Tests;

public class BoardTests
{
    private static (Board Board, BoardColumn Todo, BoardColumn Doing, BoardColumn Done) NewBoard()
    {
        var board = Board.CreateDefault("Teszt");
        var todo = board.Columns[0];
        foreach (var title in new[] { "A", "B", "C" }) todo.Cards.Add(new Card { Title = title });
        return (board, todo, board.Columns[1], board.Columns[2]);
    }

    [Fact]
    public void Reorders_within_a_column()
    {
        var (board, todo, _, _) = NewBoard();
        var a = todo.Cards[0];

        Assert.Equal(MoveResult.Moved, BoardOperations.MoveCard(board, a.Id, todo, 3));
        Assert.Equal(new[] { "B", "C", "A" }, todo.Cards.Select(c => c.Title));

        Assert.Equal(MoveResult.Unchanged, BoardOperations.MoveCard(board, a.Id, todo, 3));
    }

    [Fact]
    public void Moving_to_done_column_marks_done_and_back_clears_it()
    {
        var (board, todo, doing, done) = NewBoard();
        var card = todo.Cards[1];

        BoardOperations.MoveCard(board, card.Id, done, 0);
        Assert.True(card.IsDone);

        BoardOperations.MoveCard(board, card.Id, doing, 0);
        Assert.False(card.IsDone);
    }

    [Fact]
    public void Wip_limit_blocks_moves_into_full_column_but_not_reordering()
    {
        var (board, todo, doing, _) = NewBoard();
        doing.CardLimit = 1;
        doing.Cards.Add(new Card { Title = "X" });

        Assert.Equal(MoveResult.ColumnFull, BoardOperations.MoveCard(board, todo.Cards[0].Id, doing, 0));
        Assert.Equal(3, todo.Cards.Count);

        todo.CardLimit = 3;   // full, but reordering inside is fine
        Assert.Equal(MoveResult.Moved, BoardOperations.MoveCard(board, todo.Cards[2].Id, todo, 0));
    }

    [Fact]
    public void Columns_can_be_reordered()
    {
        var (board, _, _, done) = NewBoard();
        Assert.True(BoardOperations.MoveColumn(board, done.Id, 0));
        Assert.Equal("Kész", board.Columns[0].Name);
    }

    [Fact]
    public void Clone_gets_new_ids_and_remaps_labels()
    {
        var (board, todo, _, _) = NewBoard();
        todo.Cards[0].LabelIds.Add(board.Labels[0].Id);
        var workspace = new Workspace { Name = "W", Boards = { board } };

        var copy = BoardOperations.CloneWithNewIds(workspace);
        var copiedBoard = copy.Boards[0];
        var copiedCard = copiedBoard.Columns[0].Cards[0];

        Assert.NotEqual(workspace.Id, copy.Id);
        Assert.NotEqual(todo.Cards[0].Id, copiedCard.Id);
        Assert.Equal(copiedBoard.Labels[0].Id, Assert.Single(copiedCard.LabelIds));
    }

    [Fact]
    public void Due_state_classification()
    {
        var now = new DateTime(2026, 10, 14, 12, 0, 0);
        Assert.Equal(DueState.Overdue, CardDue.Classify(new Card { DueDate = new DateOnly(2026, 10, 13) }, now));
        Assert.Equal(DueState.DueSoon, CardDue.Classify(new Card { DueDate = new DateOnly(2026, 10, 14) }, now));
        Assert.Equal(DueState.Upcoming, CardDue.Classify(new Card { DueDate = new DateOnly(2026, 10, 20) }, now));
        Assert.Equal(DueState.Done, CardDue.Classify(new Card { DueDate = new DateOnly(2026, 10, 13), IsDone = true }, now));
    }

    [Fact]
    public void Workspace_round_trips_through_openKanban_file()
    {
        var (board, todo, _, _) = NewBoard();
        todo.CardLimit = 5;
        var card = todo.Cards[0];
        card.DueDate = new DateOnly(2026, 10, 20);
        card.DueTime = new TimeOnly(9, 30);
        card.Checklist.Add(new ChecklistItem { Text = "lépés", IsDone = true });
        card.Note = new LinkedNote(Guid.NewGuid(), "Jegyzet");
        var workspace = new Workspace { Name = "Munka", Boards = { board } };

        var serializer = new OpenKanbanSerializer();
        using var stream = new MemoryStream();
        serializer.Write(workspace, stream);
        stream.Position = 0;
        var copy = serializer.Read(stream);

        var copiedColumn = copy.Boards[0].Columns[0];
        var copiedCard = copiedColumn.Cards[0];
        Assert.Equal(workspace.Id, copy.Id);
        Assert.Equal(5, copiedColumn.CardLimit);
        Assert.Equal(new TimeOnly(9, 30), copiedCard.DueTime);
        Assert.True(Assert.Single(copiedCard.Checklist).IsDone);
        Assert.Equal("Jegyzet", copiedCard.Note!.Title);
        Assert.Equal(4, copy.Boards[0].Labels.Count);
    }
}
