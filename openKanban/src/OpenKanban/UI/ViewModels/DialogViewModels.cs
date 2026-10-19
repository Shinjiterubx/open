using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenKanban.Core.Models;

namespace OpenKanban.UI.ViewModels;

/// <summary>Name, colour and description of a workspace.</summary>
public sealed partial class WorkspaceDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _color;
    [ObservableProperty] private string _description;

    public WorkspaceDialogViewModel(string title, string name, string color, string? description)
    {
        Title = title;
        _name = name;
        _color = color;
        _description = description ?? string.Empty;
    }

    public string Title { get; }
    public IReadOnlyList<string> Palette => Workspace.Palette;
}

/// <summary>Column settings: name, colour, WIP limit, "done" column.</summary>
public sealed partial class ColumnDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _color;
    [ObservableProperty] private bool _hasLimit;
    [ObservableProperty] private string _limitText;
    [ObservableProperty] private bool _marksDone;
    [ObservableProperty] private string _errorText = string.Empty;

    public ColumnDialogViewModel(BoardColumn column)
    {
        _name = column.Name;
        _color = column.Color;
        _hasLimit = column.CardLimit is not null;
        _limitText = (column.CardLimit ?? Math.Max(5, column.Cards.Count)).ToString();
        _marksDone = column.MarksCardsDone;
        CurrentCount = column.Cards.Count;
    }

    public IReadOnlyList<string> Palette => BoardColumn.Palette;
    public int CurrentCount { get; }

    /// <summary>Validated limit (null = unlimited). Returns false with <see cref="ErrorText"/> on invalid input.</summary>
    public bool TryGetLimit(out int? limit)
    {
        limit = null;
        ErrorText = string.Empty;
        if (!HasLimit) return true;

        if (!int.TryParse(LimitText, out var value) || value < 1 || value > 999)
        {
            ErrorText = "A korlát 1 és 999 közötti szám lehet.";
            return false;
        }

        limit = value;
        return true;
    }

    public string LimitHint => $"Jelenleg {CurrentCount} kártya van az oszlopban. Ha megtelik, újat nem lehet hozzáadni vagy ide húzni.";
}

/// <summary>An editable label row in the board dialog.</summary>
public sealed partial class LabelEditViewModel : ObservableObject
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _color;

    public LabelEditViewModel(CardLabel label)
    {
        Id = label.Id;
        _name = label.Name;
        _color = label.Color;
    }

    public Guid Id { get; }
    public IReadOnlyList<string> Palette => CardLabel.Palette;

    public CardLabel ToModel() => new() { Id = Id, Name = Name.Trim(), Color = Color };
}

/// <summary>Board settings: name, background, labels.</summary>
public sealed partial class BoardDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _background;

    public BoardDialogViewModel(Board board)
    {
        _name = board.Name;
        _background = board.Background;
        Labels = new ObservableCollection<LabelEditViewModel>(board.Labels.Select(l => new LabelEditViewModel(l)));
    }

    public IReadOnlyList<string> Backgrounds => Board.Backgrounds;
    public ObservableCollection<LabelEditViewModel> Labels { get; }

    [RelayCommand]
    private void AddLabel() =>
        Labels.Add(new LabelEditViewModel(new CardLabel { Name = string.Empty, Color = CardLabel.Palette[Labels.Count % CardLabel.Palette.Length] }));

    [RelayCommand]
    private void RemoveLabel(LabelEditViewModel? label)
    {
        if (label is not null) Labels.Remove(label);
    }

    public IReadOnlyList<CardLabel> BuildLabels() => Labels.Select(l => l.ToModel()).ToList();
}
