using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenKanban.Core.Abstractions;
using OpenKanban.Core.Boards;
using OpenKanban.Core.Models;
using OpenKanban.Core.Time;
using OpenKanban.UI.Services;

namespace OpenKanban.UI.ViewModels;

public enum CardEditorOutcome
{
    Cancelled,
    Saved,
    Deleted,
}

public sealed record ColumnChoice(Guid Id, string Name, string Color, bool IsFull);

public sealed record NoteChoice(Guid? Id, string Title)
{
    public override string ToString() => Title;
}

public sealed partial class LabelToggleViewModel : ObservableObject
{
    [ObservableProperty] private bool _isChecked;

    public LabelToggleViewModel(CardLabel label, bool isChecked)
    {
        Label = label;
        _isChecked = isChecked;
    }

    public CardLabel Label { get; }
    public string Name => string.IsNullOrWhiteSpace(Label.Name) ? "(címke)" : Label.Name;
    public string Color => Label.Color;
}

public sealed partial class ChecklistItemViewModel : ObservableObject
{
    [ObservableProperty] private string _text;
    [ObservableProperty] private bool _isDone;

    public ChecklistItemViewModel(ChecklistItem item)
    {
        Id = item.Id;
        _text = item.Text;
        _isDone = item.IsDone;
    }

    public Guid Id { get; }

    public ChecklistItem ToModel() => new() { Id = Id, Text = Text.Trim(), IsDone = IsDone };
}

/// <summary>Card details: title, column, labels, deadline, cover, checklist, description, linked note.</summary>
public sealed partial class CardEditorViewModel : ObservableObject
{
    private readonly Card _original;
    private readonly IDialogService _dialogs;
    private readonly ICompanionLauncher _companions;
    private readonly Guid _originalColumnId;

    [ObservableProperty] private string _title;
    [ObservableProperty] private string _description;
    [ObservableProperty] private ColumnChoice? _selectedColumn;
    [ObservableProperty] private string? _coverColor;
    [ObservableProperty] private DateTime? _dueDate;
    [ObservableProperty] private string _dueTimeText;
    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private string _newChecklistText = string.Empty;
    [ObservableProperty] private NoteChoice? _selectedNote;
    [ObservableProperty] private string _errorText = string.Empty;

    public CardEditorViewModel(
        Workspace workspace,
        Board board,
        BoardColumn column,
        Card card,
        IReadOnlyList<NoteReference> notes,
        IDialogService dialogs,
        ICompanionLauncher companions)
    {
        _original = card;
        _dialogs = dialogs;
        _companions = companions;
        _originalColumnId = column.Id;

        Breadcrumb = $"{workspace.Name}  ›  {board.Name}";
        _title = card.Title;
        _description = card.Description ?? string.Empty;
        _coverColor = card.CoverColor;
        _dueDate = card.DueDate?.ToDateTime(TimeOnly.MinValue);
        _dueTimeText = card.DueTime?.ToString("HH:mm") ?? string.Empty;
        _isDone = card.IsDone;

        Columns = board.Columns.Select(c => new ColumnChoice(c.Id, c.Name, c.Color, c.IsFull && c.Id != column.Id)).ToList();
        _selectedColumn = Columns.First(c => c.Id == column.Id);

        Labels = board.Labels.Select(l => new LabelToggleViewModel(l, card.LabelIds.Contains(l.Id))).ToList();
        Checklist = new ObservableCollection<ChecklistItemViewModel>(card.Checklist.Select(i => new ChecklistItemViewModel(i)));
        Checklist.CollectionChanged += (_, _) => RaiseChecklist();
        foreach (var item in Checklist) item.PropertyChanged += (_, _) => RaiseChecklist();

        NoteChoices = new[] { new NoteChoice(null, "Nincs kapcsolt jegyzet") }
            .Concat(notes.Select(n => new NoteChoice(n.Id, n.DisplayTitle)))
            .ToList();
        if (card.Note is { } linked)
        {
            _selectedNote = NoteChoices.FirstOrDefault(n => n.Id == linked.NoteId);
            if (_selectedNote is null)
            {
                _selectedNote = new NoteChoice(linked.NoteId, linked.Title + " (nem elérhető)");
                NoteChoices = NoteChoices.Append(_selectedNote).ToList();
            }
        }
        else
        {
            _selectedNote = NoteChoices[0];
        }
    }

    public event EventHandler? CloseRequested;

    public CardEditorOutcome Outcome { get; private set; } = CardEditorOutcome.Cancelled;
    public Card? Result { get; private set; }
    public Guid? TargetColumnId => SelectedColumn?.Id is { } id && id != _originalColumnId ? id : null;

    public string Breadcrumb { get; }
    public IReadOnlyList<ColumnChoice> Columns { get; }
    public IReadOnlyList<LabelToggleViewModel> Labels { get; }
    public bool HasLabels => Labels.Count > 0;
    public ObservableCollection<ChecklistItemViewModel> Checklist { get; }
    public IReadOnlyList<NoteChoice> NoteChoices { get; private set; }
    public IReadOnlyList<string> CoverPalette => CardLabel.Palette;

    public bool IsNotesAvailable => _companions.IsOpenNotesAvailable;
    public bool IsCalendarAvailable => _companions.IsOpenCalendarAvailable;
    public bool HasLinkedNote => SelectedNote?.Id is not null;
    public bool HasDue => DueDate is not null;
    public bool HasCover => !string.IsNullOrEmpty(CoverColor);

    public bool HasChecklist => Checklist.Count > 0;
    public double ChecklistProgress => Checklist.Count == 0 ? 0 : 100.0 * Checklist.Count(i => i.IsDone) / Checklist.Count;
    public string ChecklistProgressText => $"{Checklist.Count(i => i.IsDone)} / {Checklist.Count}";

    public string DueStatusText
    {
        get
        {
            if (DueDate is null) return string.Empty;
            var probe = new Card { DueDate = DateOnly.FromDateTime(DueDate.Value), IsDone = IsDone };
            if (TimeInput.TryParse(DueTimeText, out var time)) probe.DueTime = time;
            return CardDue.Classify(probe, DateTime.Now) switch
            {
                DueState.Done => "Kész",
                DueState.Overdue => "Lejárt",
                DueState.DueSoon => "Hamarosan esedékes",
                _ => string.Empty,
            };
        }
    }

    // ───────────────────────── commands ─────────────────────────

    [RelayCommand]
    private void AddChecklistItem()
    {
        var text = NewChecklistText.Trim();
        if (text.Length == 0) return;
        var item = new ChecklistItemViewModel(new ChecklistItem { Text = text });
        item.PropertyChanged += (_, _) => RaiseChecklist();
        Checklist.Add(item);
        NewChecklistText = string.Empty;
    }

    [RelayCommand]
    private void RemoveChecklistItem(ChecklistItemViewModel? item)
    {
        if (item is not null) Checklist.Remove(item);
    }

    [RelayCommand]
    private void SetDueInDays(string? days)
    {
        if (int.TryParse(days, out var offset)) DueDate = DateTime.Today.AddDays(offset);
    }

    [RelayCommand]
    private void ClearDue()
    {
        DueDate = null;
        DueTimeText = string.Empty;
    }

    [RelayCommand]
    private void ClearCover() => CoverColor = null;

    [RelayCommand]
    private void ClearNote() => SelectedNote = NoteChoices[0];

    [RelayCommand]
    private void OpenNote()
    {
        if (SelectedNote?.Id is not { } id) return;
        if (!_companions.TryOpenNote(id))
            _dialogs.ShowError("openNotes nem található", "Telepítsd az openNotes alkalmazást (pl. az openLauncherből), és indítsd el egyszer.");
    }

    [RelayCommand]
    private void ShowInCalendar()
    {
        if (DueDate is not { } date) return;
        if (!_companions.TryShowInCalendar(DateOnly.FromDateTime(date)))
            _dialogs.ShowError("openCalendar nem található", "Telepítsd az openCalendar alkalmazást (pl. az openLauncherből), és indítsd el egyszer.");
    }

    [RelayCommand]
    private void Save()
    {
        ErrorText = string.Empty;
        if (string.IsNullOrWhiteSpace(Title))
        {
            ErrorText = "Adj címet a kártyának.";
            return;
        }
        if (!TimeInput.TryParse(DueTimeText, out var time))
        {
            ErrorText = "Érvénytelen időpont (pl. 14:30).";
            return;
        }
        if (SelectedColumn is { IsFull: true })
        {
            ErrorText = $"A(z) „{SelectedColumn.Name}” oszlop megtelt – válassz másikat, vagy emeld a korlátot.";
            return;
        }

        var card = _original.Clone();
        card.Title = Title.Trim();
        card.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.TrimEnd();
        card.CoverColor = string.IsNullOrEmpty(CoverColor) ? null : CoverColor;
        card.LabelIds = Labels.Where(l => l.IsChecked).Select(l => l.Label.Id).ToList();
        card.Checklist = Checklist.Where(i => !string.IsNullOrWhiteSpace(i.Text)).Select(i => i.ToModel()).ToList();
        card.DueDate = DueDate is { } d ? DateOnly.FromDateTime(d) : null;
        card.DueTime = card.DueDate is null ? null : time;
        card.IsDone = IsDone;
        card.Note = SelectedNote?.Id is { } noteId
            ? new LinkedNote(noteId, SelectedNote.Title.Replace(" (nem elérhető)", string.Empty))
            : null;

        Result = card;
        Outcome = CardEditorOutcome.Saved;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Delete()
    {
        if (!_dialogs.Confirm("Kártya törlése", $"Biztosan törlöd: „{_original.DisplayTitle}”?", "Törlés", destructive: true)) return;
        Outcome = CardEditorOutcome.Deleted;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        Outcome = CardEditorOutcome.Cancelled;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnDueDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(HasDue));
        OnPropertyChanged(nameof(DueStatusText));
    }

    partial void OnDueTimeTextChanged(string value) => OnPropertyChanged(nameof(DueStatusText));
    partial void OnIsDoneChanged(bool value) => OnPropertyChanged(nameof(DueStatusText));
    partial void OnCoverColorChanged(string? value) => OnPropertyChanged(nameof(HasCover));
    partial void OnSelectedNoteChanged(NoteChoice? value) => OnPropertyChanged(nameof(HasLinkedNote));

    private void RaiseChecklist()
    {
        OnPropertyChanged(nameof(HasChecklist));
        OnPropertyChanged(nameof(ChecklistProgress));
        OnPropertyChanged(nameof(ChecklistProgressText));
    }
}
