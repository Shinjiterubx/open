using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenKanban.Core.Abstractions;
using OpenKanban.Core.Boards;
using OpenKanban.Core.Models;
using OpenKanban.Infrastructure.Storage;
using OpenKanban.Services;
using OpenKanban.UI.Services;

namespace OpenKanban.UI.ViewModels;

/// <summary>Workspaces / boards navigation, the open board and every board, column and card command.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly KanbanService _service;
    private readonly ISettingsStore _settings;
    private readonly IDialogService _dialogs;
    private readonly IWindowService _windows;
    private readonly INoteSource _notes;
    private readonly ICompanionLauncher _companions;
    private readonly IAppPaths _paths;

    [ObservableProperty] private WorkspaceItemViewModel? _selectedWorkspace;
    [ObservableProperty] private BoardItemViewModel? _selectedBoard;
    [ObservableProperty] private BoardViewModel? _currentBoard;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;

    public MainViewModel(KanbanService service, ISettingsStore settings, IDialogService dialogs, IWindowService windows,
        INoteSource notes, ICompanionLauncher companions, IAppPaths paths)
    {
        _service = service;
        _settings = settings;
        _dialogs = dialogs;
        _windows = windows;
        _notes = notes;
        _companions = companions;
        _paths = paths;

        _service.Changed += (_, workspace) => OnDataChanged(workspace);
        _settings.Saved += (_, _) =>
        {
            OnPropertyChanged(nameof(ColumnWidth));
            OnPropertyChanged(nameof(FadeDoneCards));
            RebuildBoard();
        };
    }

    public ObservableCollection<WorkspaceItemViewModel> Workspaces { get; } = new();

    public bool HasWorkspaces => Workspaces.Count > 0;
    public bool IsBoardOpen => CurrentBoard is not null;
    public bool IsOverviewOpen => CurrentBoard is null && SelectedWorkspace is not null;
    public double ColumnWidth => _settings.Current.ColumnWidth;
    public bool FadeDoneCards => _settings.Current.FadeDoneCards;

    public void Initialize()
    {
        _service.Load();
        if (_service.LoadErrors.Count > 0)
            _dialogs.ShowError("Néhány workspace nem olvasható", string.Join("\n", _service.LoadErrors));

        var settings = _settings.Current;
        var board = settings.LastBoardId is { } boardId ? FindBoardItem(boardId) : null;
        if (board is not null) OpenBoard(board);
        else SelectedWorkspace = Workspaces.FirstOrDefault(w => w.Id == settings.LastWorkspaceId) ?? Workspaces.FirstOrDefault();
    }

    // ───────────────────────── navigation ─────────────────────────

    [RelayCommand]
    private void OpenWorkspace(WorkspaceItemViewModel? workspace)
    {
        if (workspace is null) return;
        SelectedBoard = null;
        SelectedWorkspace = workspace;
    }

    [RelayCommand]
    private void OpenBoard(BoardItemViewModel? board)
    {
        if (board is null) return;
        SelectedWorkspace = board.Workspace;
        SelectedBoard = board;
    }

    [RelayCommand]
    private void ToggleWorkspace(WorkspaceItemViewModel? workspace)
    {
        if (workspace is not null) workspace.IsExpanded = !workspace.IsExpanded;
    }

    /// <summary>Opens a card by id (used by <c>--open-card</c> from openCalendar).</summary>
    public bool OpenCardById(Guid cardId)
    {
        if (_service.FindCard(cardId) is not { } found) return false;
        if (FindBoardItem(found.Location.Board.Id) is { } board) OpenBoard(board);
        EditCard(found.Location.Card.Id);
        return true;
    }

    partial void OnSelectedWorkspaceChanged(WorkspaceItemViewModel? value)
    {
        foreach (var workspace in Workspaces) workspace.IsSelected = workspace == value && SelectedBoard is null;
        _settings.Current.LastWorkspaceId = value?.Id;
        RaiseViewState();
    }

    partial void OnSelectedBoardChanged(BoardItemViewModel? value)
    {
        foreach (var board in Workspaces.SelectMany(w => w.Boards)) board.IsSelected = board == value;
        foreach (var workspace in Workspaces) workspace.IsSelected = workspace == SelectedWorkspace && value is null;
        _settings.Current.LastBoardId = value?.Id;
        SearchText = string.Empty;
        RebuildBoard();
    }

    partial void OnSearchTextChanged(string value) => RebuildBoard();

    partial void OnCurrentBoardChanged(BoardViewModel? value) => RaiseViewState();

    // ───────────────────────── workspaces ─────────────────────────

    [RelayCommand]
    private void NewWorkspace()
    {
        var dialog = new WorkspaceDialogViewModel("Új workspace", string.Empty, Workspace.Palette[Workspaces.Count % Workspace.Palette.Length], null);
        if (!_windows.ShowWorkspaceDialog(dialog)) return;

        Workspace? created = null;
        TryRun("Mentési hiba", () => created = _service.AddWorkspace(dialog.Name, dialog.Color, dialog.Description));
        if (created is not null && Workspaces.FirstOrDefault(w => w.Id == created.Id) is { } item) OpenWorkspace(item);
    }

    [RelayCommand]
    private void EditWorkspace(WorkspaceItemViewModel? workspace)
    {
        workspace ??= SelectedWorkspace;
        if (workspace is null) return;
        var dialog = new WorkspaceDialogViewModel("Workspace szerkesztése", workspace.Name, workspace.Color, workspace.Description);
        if (_windows.ShowWorkspaceDialog(dialog))
            TryRun("Mentési hiba", () => _service.UpdateWorkspace(workspace.Model, dialog.Name, dialog.Color, dialog.Description));
    }

    [RelayCommand]
    private void DeleteWorkspace(WorkspaceItemViewModel? workspace)
    {
        if (workspace is null) return;
        if (!_dialogs.Confirm("Workspace törlése",
                $"Biztosan törlöd a(z) „{workspace.Name}” workspace-t az összes táblájával?\nA fájl a Kuka mappába kerül, onnan visszaállítható.",
                "Törlés", destructive: true))
            return;
        TryRun("Törlési hiba", () => _service.DeleteWorkspace(workspace.Model));
    }

    [RelayCommand]
    private void ExportWorkspace(WorkspaceItemViewModel? workspace)
    {
        workspace ??= SelectedWorkspace;
        if (workspace is null) return;

        var ext = OpenKanbanSerializer.FileExtension;
        var path = _dialogs.PickSaveFile("Workspace exportálása", $"openKanban workspace (*{ext})|*{ext}", SafeFileName(workspace.Name) + ext);
        if (path is not null) TryRun("Exportálási hiba", () => _service.Export(workspace.Model, path));
    }

    [RelayCommand]
    private void Import()
    {
        var ext = OpenKanbanSerializer.FileExtension;
        var files = _dialogs.PickFilesToOpen("Workspace importálása", $"openKanban workspace (*{ext})|*{ext}");
        if (files.Count > 0) ImportFiles(files);
    }

    /// <summary>Imports .openKanban files (dialog, drag &amp; drop, file association).</summary>
    public void ImportFiles(IEnumerable<string> paths)
    {
        var errors = new List<string>();
        Workspace? last = null;

        foreach (var path in paths.Where(p => p.EndsWith(OpenKanbanSerializer.FileExtension, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var workspace = _service.ReadFile(path);
                var replace = false;
                if (_service.FindWorkspace(workspace.Id) is { } existing)
                {
                    var choice = _dialogs.Ask("A workspace már létezik",
                        $"A(z) „{existing.Name}” workspace már szerepel. Felülírod az importált változattal, vagy másolatként adod hozzá?",
                        "Felülírás", "Másolatként");
                    if (choice == DialogChoice.Cancel) continue;
                    replace = choice == DialogChoice.Primary;
                }
                last = _service.Import(workspace, replace);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        if (last is not null && Workspaces.FirstOrDefault(w => w.Id == last.Id) is { } item) OpenWorkspace(item);
        if (errors.Count > 0) _dialogs.ShowError("Importálási hiba", string.Join("\n", errors));
    }

    // ───────────────────────── boards ─────────────────────────

    [RelayCommand]
    private void NewBoard(WorkspaceItemViewModel? workspace)
    {
        workspace ??= SelectedWorkspace;
        if (workspace is null) return;

        Board? board = null;
        TryRun("Mentési hiba", () => board = _service.AddBoard(workspace.Model, "Új tábla"));
        if (board is not null && FindBoardItem(board.Id) is { } item)
        {
            OpenBoard(item);
            EditBoard(item);
        }
    }

    [RelayCommand]
    private void EditBoard(BoardItemViewModel? board)
    {
        board ??= SelectedBoard;
        if (board is null) return;

        var dialog = new BoardDialogViewModel(board.Model);
        if (_windows.ShowBoardDialog(dialog))
            TryRun("Mentési hiba", () => _service.UpdateBoard(board.Workspace.Model, board.Model, dialog.Name, dialog.Background, dialog.BuildLabels()));
    }

    [RelayCommand]
    private void DuplicateBoard(BoardItemViewModel? board)
    {
        board ??= SelectedBoard;
        if (board is null) return;
        Board? copy = null;
        TryRun("Mentési hiba", () => copy = _service.DuplicateBoard(board.Workspace.Model, board.Model));
        if (copy is not null && FindBoardItem(copy.Id) is { } item) OpenBoard(item);
    }

    [RelayCommand]
    private void DeleteBoard(BoardItemViewModel? board)
    {
        board ??= SelectedBoard;
        if (board is null) return;
        if (!_dialogs.Confirm("Tábla törlése", $"Biztosan törlöd a(z) „{board.Name}” táblát és {board.CardCount} kártyáját?", "Törlés", destructive: true))
            return;

        var workspace = board.Workspace;
        TryRun("Törlési hiba", () => _service.DeleteBoard(workspace.Model, board.Model));
        if (Workspaces.FirstOrDefault(w => w.Id == workspace.Id) is { } item) OpenWorkspace(item);
    }

    // ───────────────────────── columns ─────────────────────────

    [RelayCommand]
    private void NewColumn()
    {
        if (CurrentBoard is null) return;
        BoardColumn? column = null;
        TryRun("Mentési hiba", () => column = _service.AddColumn(CurrentBoard.Workspace, CurrentBoard.Board, "Új oszlop"));
        if (column is not null && CurrentBoard?.Columns.FirstOrDefault(c => c.Id == column.Id) is { } item) EditColumn(item);
    }

    [RelayCommand]
    private void EditColumn(ColumnViewModel? column)
    {
        if (column is null || CurrentBoard is null) return;
        var dialog = new ColumnDialogViewModel(column.Model);
        var workspace = CurrentBoard.Workspace;
        if (_windows.ShowColumnDialog(dialog) && dialog.TryGetLimit(out var limit))
            TryRun("Mentési hiba", () => _service.UpdateColumn(workspace, column.Model, dialog.Name, dialog.Color, limit, dialog.MarksDone));
    }

    [RelayCommand]
    private void DeleteColumn(ColumnViewModel? column)
    {
        if (column is null || CurrentBoard is null) return;
        var count = column.Model.Cards.Count;
        if (count > 0 && !_dialogs.Confirm("Oszlop törlése",
                $"A(z) „{column.Name}” oszlopban {count} kártya van. Biztosan törlöd az oszlopot a kártyákkal együtt?", "Törlés", destructive: true))
            return;
        TryRun("Törlési hiba", () => _service.DeleteColumn(CurrentBoard.Workspace, CurrentBoard.Board, column.Model));
    }

    /// <summary>Drag &amp; drop of a column header.</summary>
    public void MoveColumn(Guid columnId, int index)
    {
        if (CurrentBoard is null) return;
        TryRun("Mentési hiba", () => _service.MoveColumn(CurrentBoard.Workspace, CurrentBoard.Board, columnId, index));
    }

    // ───────────────────────── cards ─────────────────────────

    [RelayCommand]
    private void OpenComposer(ColumnViewModel? column)
    {
        if (column is null || CurrentBoard is null) return;
        if (column.IsFull)
        {
            ShowColumnFull(column.Model);
            return;
        }
        foreach (var other in CurrentBoard.Columns) other.IsComposerOpen = other == column;
    }

    [RelayCommand]
    private void CloseComposer(ColumnViewModel? column)
    {
        if (column is null) return;
        column.IsComposerOpen = false;
        column.ComposerText = string.Empty;
    }

    /// <summary>Adds the composer text as a new card; the composer stays open for the next one.</summary>
    [RelayCommand]
    private void AddCard(ColumnViewModel? column)
    {
        if (column is null || CurrentBoard is null) return;
        var title = column.ComposerText.Trim();
        if (title.Length == 0) return;

        column.ComposerText = string.Empty;
        Card? card = null;
        TryRun("Mentési hiba", () => card = _service.AddCard(CurrentBoard.Workspace, column.Model, title));
        if (card is null)
        {
            column.ComposerText = title;
            ShowColumnFull(column.Model);
        }
    }

    [RelayCommand]
    private void OpenCard(CardViewModel? card)
    {
        if (card is not null) EditCard(card.Id);
    }

    [RelayCommand]
    private void ToggleCardDone(CardViewModel? card)
    {
        if (card is not null) TryRun("Mentési hiba", () => _service.SetCardDone(card.Id, !card.IsDone));
    }

    [RelayCommand]
    private void OpenCardNote(CardViewModel? card)
    {
        if (card?.Model.Note is not { } note) return;
        if (!_companions.TryOpenNote(note.NoteId))
            _dialogs.ShowError("openNotes nem található", "Telepítsd az openNotes alkalmazást (pl. az openLauncherből), és indítsd el egyszer.");
    }

    /// <summary>Drag &amp; drop of a card. Returns <c>false</c> if the target column is full.</summary>
    public bool MoveCard(Guid cardId, Guid columnId, int index)
    {
        var result = MoveResult.Unchanged;
        TryRun("Mentési hiba", () => result = _service.MoveCard(cardId, columnId, index));

        if (result != MoveResult.ColumnFull) return true;
        if (CurrentBoard?.Board.Columns.FirstOrDefault(c => c.Id == columnId) is { } column) ShowColumnFull(column);
        return false;
    }

    private void EditCard(Guid cardId)
    {
        if (_service.FindCard(cardId) is not { } found) return;
        var (workspace, location) = found;

        var notes = _settings.Current.OpenNotesIntegration ? _notes.LoadNotes() : Array.Empty<NoteReference>();
        var editor = new CardEditorViewModel(workspace, location.Board, location.Column, location.Card, notes, _dialogs, _companions);
        _windows.ShowCardEditor(editor);

        TryRun("Mentési hiba", () =>
        {
            switch (editor.Outcome)
            {
                case CardEditorOutcome.Saved when editor.Result is not null:
                    if (_service.SaveCard(editor.Result, editor.TargetColumnId) == MoveResult.ColumnFull)
                        _dialogs.ShowError("Az oszlop megtelt", "A kártya mentve, de a kiválasztott oszlopba már nem fér több kártya.");
                    break;
                case CardEditorOutcome.Deleted:
                    _service.DeleteCard(cardId);
                    break;
            }
        });
    }

    // ───────────────────────── app ─────────────────────────

    [RelayCommand]
    private void OpenSettings() => _windows.ShowSettings();

    [RelayCommand]
    private void OpenDataFolder() => Process.Start(new ProcessStartInfo(_paths.WorkspacesDirectory) { UseShellExecute = true });

    // ───────────────────────── data → view ─────────────────────────

    private void OnDataChanged(Workspace? changed)
    {
        if (changed is null)
        {
            RebuildNavigation();
        }
        else if (Workspaces.FirstOrDefault(w => w.Id == changed.Id) is { } item)
        {
            // Cheap refresh of counters in the sidebar / overview.
            var index = Workspaces.IndexOf(item);
            var selectedBoardId = SelectedBoard?.Id;
            var expanded = item.IsExpanded;
            var replacement = new WorkspaceItemViewModel(changed) { IsExpanded = expanded };
            Workspaces[index] = replacement;

            if (SelectedWorkspace?.Id == changed.Id) SetSelectionSilently(replacement, selectedBoardId);
        }

        RebuildBoard();
        OnPropertyChanged(nameof(HasWorkspaces));
    }

    private void RebuildNavigation()
    {
        var expanded = Workspaces.ToDictionary(w => w.Id, w => w.IsExpanded);
        var selectedWorkspaceId = SelectedWorkspace?.Id;
        var selectedBoardId = SelectedBoard?.Id;

        Workspaces.Clear();
        foreach (var workspace in _service.Workspaces)
            Workspaces.Add(new WorkspaceItemViewModel(workspace) { IsExpanded = !expanded.TryGetValue(workspace.Id, out var e) || e });

        var selected = Workspaces.FirstOrDefault(w => w.Id == selectedWorkspaceId) ?? Workspaces.FirstOrDefault();
        SetSelectionSilently(selected, selectedBoardId);
    }

    /// <summary>Re-points the selection to rebuilt items without resetting the search / composer state.</summary>
    private void SetSelectionSilently(WorkspaceItemViewModel? workspace, Guid? boardId)
    {
        var board = boardId is { } id ? workspace?.Boards.FirstOrDefault(b => b.Id == id) : null;
        SetProperty(ref _selectedWorkspace, workspace, nameof(SelectedWorkspace));
        SetProperty(ref _selectedBoard, board, nameof(SelectedBoard));

        foreach (var w in Workspaces) w.IsSelected = w == workspace && board is null;
        foreach (var b in Workspaces.SelectMany(w => w.Boards)) b.IsSelected = b == board;
        RaiseViewState();
    }

    private void RebuildBoard()
    {
        if (SelectedBoard is null || _service.FindBoard(SelectedBoard.Id) is not { } found)
        {
            CurrentBoard = null;
            StatusText = string.Empty;
            return;
        }

        var uiState = CurrentBoard?.Board.Id == found.Board.Id
            ? CurrentBoard.CaptureUiState()
            : new Dictionary<Guid, ColumnUiState>();
        CurrentBoard = new BoardViewModel(found.Workspace, found.Board, SearchText, DateTime.Now, uiState);

        var cards = found.Board.Columns.SelectMany(c => c.Cards).ToList();
        var overdue = cards.Count(c => CardDue.Classify(c, DateTime.Now) == DueState.Overdue);
        StatusText = $"{cards.Count} kártya · {cards.Count(c => !c.IsDone)} nyitott"
                     + (overdue > 0 ? $" · {overdue} lejárt" : string.Empty)
                     + (string.IsNullOrWhiteSpace(SearchText) ? string.Empty : $" · szűrés: „{SearchText}”");
    }

    private void RaiseViewState()
    {
        OnPropertyChanged(nameof(IsBoardOpen));
        OnPropertyChanged(nameof(IsOverviewOpen));
    }

    private BoardItemViewModel? FindBoardItem(Guid boardId) =>
        Workspaces.SelectMany(w => w.Boards).FirstOrDefault(b => b.Id == boardId);

    private void ShowColumnFull(BoardColumn column) =>
        _dialogs.ShowError("Az oszlop megtelt",
            $"A(z) „{column.Name}” oszlopba legfeljebb {column.CardLimit} kártya fér. Helyezz át vagy zárj le kártyát, vagy emeld a korlátot az oszlop beállításaiban.");

    private void TryRun(string errorTitle, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            _dialogs.ShowError(errorTitle, ex.Message);
        }
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return clean.Length == 0 ? "workspace" : clean;
    }
}
