namespace OpenKanban.Core.Models;

/// <summary>A Kanban board: ordered columns, board-level labels and a background colour.</summary>
public sealed class Board
{
    public static readonly string[] Backgrounds =
    {
        "#F3F6FA", "#EAF1F8", "#E9F5EE", "#FFF6E5", "#FDEEF2", "#F1EEFB", "#F4F4F5", "#2F5A8A", "#1F3B5C", "#2E6F57",
    };

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Tábla";
    public string Background { get; set; } = Backgrounds[0];
    public List<BoardColumn> Columns { get; set; } = new();
    public List<CardLabel> Labels { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>A new board with the classic three columns and a few labels.</summary>
    public static Board CreateDefault(string name) => new()
    {
        Name = name,
        Columns =
        {
            new BoardColumn { Name = "Teendő", Color = "#6B7280" },
            new BoardColumn { Name = "Folyamatban", Color = "#3E8FC9" },
            new BoardColumn { Name = "Kész", Color = "#2E8B57", MarksCardsDone = true },
        },
        Labels =
        {
            new CardLabel { Name = "Sürgős", Color = "#E0434F" },
            new CardLabel { Name = "Fontos", Color = "#F0B429" },
            new CardLabel { Name = "Ötlet", Color = "#8B6BD6" },
            new CardLabel { Name = "Hiba", Color = "#E58A3C" },
        },
    };
}

/// <summary>A column (list) of cards.</summary>
public sealed class BoardColumn
{
    public static readonly string[] Palette =
    {
        "#6B7280", "#3E8FC9", "#2F5A8A", "#2E8B57", "#E0B23B", "#E58A3C", "#E0434F", "#D45D79", "#8B6BD6",
    };

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Oszlop";
    public string Color { get; set; } = Palette[0];

    /// <summary>Maximum number of cards (WIP limit); <c>null</c> = unlimited.</summary>
    public int? CardLimit { get; set; }

    /// <summary>Cards moved here are marked done (e.g. the "Kész" column).</summary>
    public bool MarksCardsDone { get; set; }

    public List<Card> Cards { get; set; } = new();

    public bool IsFull => CardLimit is { } limit && Cards.Count >= limit;
}

/// <summary>Board-level label (name + colour) that cards can reference.</summary>
public sealed class CardLabel
{
    public static readonly string[] Palette =
    {
        "#E0434F", "#F0B429", "#2E8B57", "#3E8FC9", "#8B6BD6", "#E58A3C", "#D45D79", "#6B7280",
    };

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = Palette[0];
}
