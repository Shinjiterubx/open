using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenKanban.Core.Boards;
using OpenKanban.Core.Models;

namespace OpenKanban.UI.ViewModels;

/// <summary>A workspace in the sidebar with its boards.</summary>
public sealed partial class WorkspaceItemViewModel : ObservableObject
{
    [ObservableProperty] private bool _isExpanded = true;
    [ObservableProperty] private bool _isSelected;

    public WorkspaceItemViewModel(Workspace model)
    {
        Model = model;
        Boards = new ObservableCollection<BoardItemViewModel>(model.Boards.Select(b => new BoardItemViewModel(this, b)));
    }

    public Workspace Model { get; }
    public Guid Id => Model.Id;
    public string Name => Model.Name;
    public string Color => Model.Color;
    public string Initial => Model.Initial;
    public string? Description => Model.Description;
    public ObservableCollection<BoardItemViewModel> Boards { get; }
    public string Summary => $"{Model.Boards.Count} tábla · {Model.Boards.Sum(b => b.Columns.Sum(c => c.Cards.Count))} kártya";
}

/// <summary>A board entry in the sidebar / workspace overview.</summary>
public sealed partial class BoardItemViewModel : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public BoardItemViewModel(WorkspaceItemViewModel workspace, Board model)
    {
        Workspace = workspace;
        Model = model;
    }

    public WorkspaceItemViewModel Workspace { get; }
    public Board Model { get; }
    public Guid Id => Model.Id;
    public string Name => Model.Name;
    public string Background => Model.Background;
    public int CardCount => Model.Columns.Sum(c => c.Cards.Count);
    public int OpenCount => Model.Columns.Sum(c => c.Cards.Count(card => !card.IsDone));
    public string Summary => $"{Model.Columns.Count} oszlop · {OpenCount} nyitott / {CardCount} kártya";
}

/// <summary>The open board: columns with cards. Rebuilt from the model after every change.</summary>
public sealed class BoardViewModel
{
    public BoardViewModel(Workspace workspace, Board board, string filter, DateTime now, IReadOnlyDictionary<Guid, ColumnUiState> uiState)
    {
        Workspace = workspace;
        Board = board;
        Columns = new ObservableCollection<ColumnViewModel>(board.Columns.Select(c =>
            new ColumnViewModel(board, c, filter, now, uiState.TryGetValue(c.Id, out var state) ? state : null)));
    }

    public Workspace Workspace { get; }
    public Board Board { get; }
    public ObservableCollection<ColumnViewModel> Columns { get; }
    public string Name => Board.Name;
    public string WorkspaceName => Workspace.Name;
    public string Background => Board.Background;
    public bool IsDarkBackground => IsDark(Board.Background);

    public IReadOnlyDictionary<Guid, ColumnUiState> CaptureUiState() =>
        Columns.ToDictionary(c => c.Id, c => new ColumnUiState(c.IsComposerOpen, c.ComposerText));

    private static bool IsDark(string hex)
    {
        try
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            return color.R * 0.299 + color.G * 0.587 + color.B * 0.114 < 140;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>Column state that survives board rebuilds (open "add card" composer).</summary>
public sealed record ColumnUiState(bool IsComposerOpen, string ComposerText);

public sealed partial class ColumnViewModel : ObservableObject
{
    [ObservableProperty] private bool _isComposerOpen;
    [ObservableProperty] private string _composerText = string.Empty;

    public ColumnViewModel(Board board, BoardColumn model, string filter, DateTime now, ColumnUiState? state)
    {
        Model = model;
        var labels = board.Labels.ToDictionary(l => l.Id);
        Cards = new ObservableCollection<CardViewModel>(model.Cards
            .Select(c => new CardViewModel(c, labels, now))
            .Where(c => c.Matches(filter)));
        HiddenCount = model.Cards.Count - Cards.Count;

        if (state is not null)
        {
            _isComposerOpen = state.IsComposerOpen;
            _composerText = state.ComposerText;
        }
    }

    public BoardColumn Model { get; }
    public Guid Id => Model.Id;
    public string Name => Model.Name;
    public string Color => Model.Color;
    public ObservableCollection<CardViewModel> Cards { get; }
    public int HiddenCount { get; }
    public bool HasLimit => Model.CardLimit is not null;
    public bool IsFull => Model.IsFull;
    public bool IsOverLimit => Model.CardLimit is { } limit && Model.Cards.Count > limit;
    public bool MarksDone => Model.MarksCardsDone;

    public string CountText => Model.CardLimit is { } limit ? $"{Model.Cards.Count} / {limit}" : Model.Cards.Count.ToString();

    public string CountToolTip => Model.CardLimit is { } limit
        ? (IsFull ? $"Az oszlop megtelt (legfeljebb {limit} kártya)" : $"Legfeljebb {limit} kártya fér el")
        : "Kártyák száma (nincs korlát)";
}

public sealed record LabelChip(string Name, string Color)
{
    public bool HasName => !string.IsNullOrWhiteSpace(Name);
}

/// <summary>A card on the board.</summary>
public sealed class CardViewModel
{
    public CardViewModel(Card model, IReadOnlyDictionary<Guid, CardLabel> labels, DateTime now)
    {
        Model = model;
        Labels = model.LabelIds.Where(labels.ContainsKey).Select(id => new LabelChip(labels[id].Name, labels[id].Color)).ToList();
        DueState = CardDue.Classify(model, now);
        DueText = CardDue.ShortText(model, now, Hu.Culture);
    }

    public Card Model { get; }
    public Guid Id => Model.Id;
    public string Title => Model.DisplayTitle;
    public string? CoverColor => Model.CoverColor;
    public bool HasCover => !string.IsNullOrWhiteSpace(Model.CoverColor);
    public IReadOnlyList<LabelChip> Labels { get; }
    public bool HasLabels => Labels.Count > 0;
    public bool IsDone => Model.IsDone;

    public DueState DueState { get; }
    public string DueText { get; }
    public bool HasDue => Model.DueDate is not null;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Model.Description);
    public bool HasNote => Model.Note is not null;
    public bool HasChecklist => Model.Checklist.Count > 0;
    public bool IsChecklistComplete => HasChecklist && Model.Checklist.All(i => i.IsDone);
    public string ChecklistText => $"{Model.Checklist.Count(i => i.IsDone)}/{Model.Checklist.Count}";
    public bool HasBadges => HasDue || HasDescription || HasNote || HasChecklist;

    public bool Matches(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return true;
        return Model.Title.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
               || (Model.Description?.Contains(filter, StringComparison.CurrentCultureIgnoreCase) ?? false)
               || Labels.Any(l => l.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase));
    }
}
