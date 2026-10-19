using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Models;
using OpenNotes.Core.Reminders;
using OpenNotes.Infrastructure.Exchange;
using OpenNotes.UI.Services;

namespace OpenNotes.UI.ViewModels;

/// <summary>The note library: list, search, selection and note-level commands.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly INoteRepository _repository;
    private readonly NoteImportService _importService;
    private readonly IReadOnlyList<INoteExporter> _exporters;
    private readonly ISettingsStore _settings;
    private readonly IDialogService _dialogs;
    private readonly IWindowService _windows;
    private readonly IAppPaths _paths;

    [ObservableProperty] private NoteItemViewModel? _selectedNote;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private ViewMode _viewMode;
    [ObservableProperty] private double _editorFontSize;
    [ObservableProperty] private bool _isListEmpty;
    [ObservableProperty] private string _quickNoteHotkeyText = string.Empty;
    [ObservableProperty] private bool _isReminderPanelExpanded = true;
    [ObservableProperty] private ReminderCounts _reminderCounts = new(0, 0, 0);

    private string _reminderSignature = string.Empty;
    private bool _isLoading;

    public MainViewModel(
        INoteRepository repository,
        NoteImportService importService,
        IEnumerable<INoteExporter> exporters,
        ISettingsStore settings,
        IDialogService dialogs,
        IWindowService windows,
        IAppPaths paths,
        NoteEditorViewModel editor)
    {
        _repository = repository;
        _importService = importService;
        _exporters = exporters.ToList();
        _settings = settings;
        _dialogs = dialogs;
        _windows = windows;
        _paths = paths;
        Editor = editor;

        Notes = new ObservableCollection<NoteItemViewModel>();
        Notes.CollectionChanged += (_, _) => UpdateListState();

        var view = (ListCollectionView)CollectionViewSource.GetDefaultView(Notes);
        view.Filter = item => item is NoteItemViewModel note && note.Matches(SearchText);
        view.SortDescriptions.Add(new SortDescription(nameof(NoteItemViewModel.IsPinned), ListSortDirection.Descending));
        view.SortDescriptions.Add(new SortDescription(nameof(NoteItemViewModel.UpdatedAt), ListSortDirection.Descending));
        view.IsLiveSorting = true;
        view.LiveSortingProperties.Add(nameof(NoteItemViewModel.IsPinned));
        view.LiveSortingProperties.Add(nameof(NoteItemViewModel.UpdatedAt));
        NotesView = view;

        ApplySettings();
        _settings.Saved += (_, _) => ApplySettings();
        Notes.CollectionChanged += (_, _) => RefreshReminders();
        Editor.TaskStateChanged += (_, _) => RefreshReminders();
        IsReminderPanelExpanded = _settings.Current.ReminderPanelExpanded;

        Editor.SaveFailed += (_, ex) => _dialogs.ShowError("Mentési hiba", $"A jegyzet nem menthető:\n{ex.Message}");
    }

    public ObservableCollection<NoteItemViewModel> Notes { get; }
    public ICollectionView NotesView { get; }
    public NoteEditorViewModel Editor { get; }
    public string NoteCountText => Notes.Count.ToString();
    public string EmptyListText => Notes.Count == 0 ? "Még nincs jegyzeted.\nKezdj egy újat!" : "Nincs találat.";

    // ───────────────────────── reminders ─────────────────────────

    /// <summary>All active reminders: overdue → today → upcoming (priority, then due date).</summary>
    public ObservableCollection<ReminderItemViewModel> Reminders { get; } = new();
    public ObservableCollection<ReminderItemViewModel> OverdueReminders { get; } = new();
    public ObservableCollection<ReminderItemViewModel> TodayReminders { get; } = new();
    public ObservableCollection<ReminderItemViewModel> UpcomingReminders { get; } = new();

    public bool HasReminders => ReminderCounts.Total > 0;
    public bool HasOverdue => ReminderCounts.Overdue > 0;
    public bool HasToday => ReminderCounts.Today > 0;
    public bool HasUpcoming => ReminderCounts.Upcoming > 0;
    public bool HasAttention => ReminderCounts.NeedsAttention > 0;
    public string AttentionBadgeText => ReminderCounts.NeedsAttention > 99 ? "99+" : ReminderCounts.NeedsAttention.ToString();

    public string ReminderCountsText
    {
        get
        {
            var c = ReminderCounts;
            var parts = new List<string>(3);
            if (c.Overdue > 0) parts.Add($"{c.Overdue} lejárt");
            if (c.Today > 0) parts.Add($"{c.Today} mai");
            if (c.Upcoming > 0) parts.Add($"{c.Upcoming} közelgő");
            return parts.Count == 0 ? "Nincs hátralévő teendő" : string.Join(" · ", parts);
        }
    }

    public string TodayHeaderText =>
        DateTime.Now.ToString("yyyy. MMMM d., dddd", System.Globalization.CultureInfo.GetCultureInfo("hu-HU"));

    /// <summary>Rebuilds the reminder lists (on changes and periodically, as "today"/"overdue" depend on the clock).</summary>
    public void RefreshReminders()
    {
        if (_isLoading) return;

        var now = DateTime.Now;
        foreach (var note in Notes) note.RefreshTaskState();
        Editor.RefreshTimeTexts();

        var byModel = Notes.ToDictionary(n => n.Model);
        var items = ReminderRules.Collect(Notes.Select(n => n.Model), now)
            .Select(e => new ReminderItemViewModel(byModel[e.Note], e, now))
            .ToList();

        var signature = string.Join(";", items.Select(i => i.Signature));
        if (signature == _reminderSignature) return;
        _reminderSignature = signature;

        Replace(Reminders, items);
        Replace(OverdueReminders, items.Where(i => i.State == ReminderState.Overdue));
        Replace(TodayReminders, items.Where(i => i.State == ReminderState.Today));
        Replace(UpcomingReminders, items.Where(i => i.State == ReminderState.Upcoming));
        ReminderCounts = new ReminderCounts(OverdueReminders.Count, TodayReminders.Count, UpcomingReminders.Count);
        OnPropertyChanged(nameof(TodayHeaderText));
    }

    partial void OnReminderCountsChanged(ReminderCounts value)
    {
        OnPropertyChanged(nameof(HasReminders));
        OnPropertyChanged(nameof(HasOverdue));
        OnPropertyChanged(nameof(HasToday));
        OnPropertyChanged(nameof(HasUpcoming));
        OnPropertyChanged(nameof(HasAttention));
        OnPropertyChanged(nameof(AttentionBadgeText));
        OnPropertyChanged(nameof(ReminderCountsText));
    }

    partial void OnIsReminderPanelExpandedChanged(bool value) => _settings.Current.ReminderPanelExpanded = value;

    [RelayCommand]
    private void OpenReminder(ReminderItemViewModel? item)
    {
        if (item is null) return;
        SearchText = string.Empty;
        SelectedNote = item.Note;
        _windows.ShowMain();
    }

    [RelayCommand]
    private void CompleteReminder(ReminderItemViewModel? item)
    {
        if (item is null) return;

        if (ReferenceEquals(item.Note, SelectedNote))
        {
            Editor.IsDone = true;   // goes through the editor so its state and autosave stay in sync
            return;
        }

        item.Note.Model.IsDone = true;
        item.Note.Model.UpdatedAt = DateTimeOffset.Now;
        item.Note.Refresh();
        TryRun("Mentési hiba", () => _repository.Save(item.Note.Model));
        RefreshReminders();
    }

    [RelayCommand]
    private void ShowReminderSummary() => _windows.ShowReminderSummary();

    [RelayCommand]
    private void ToggleReminderPanel() => IsReminderPanelExpanded = !IsReminderPanelExpanded;

    private static void Replace(ObservableCollection<ReminderItemViewModel> target, IEnumerable<ReminderItemViewModel> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    /// <summary>Raised after a note was created so views can focus its title.</summary>
    public event EventHandler? NoteCreated;

    public void Initialize()
    {
        _isLoading = true;
        try
        {
            foreach (var note in _repository.LoadAll())
                Notes.Add(new NoteItemViewModel(note));
        }
        finally
        {
            _isLoading = false;
        }
        UpdateListState();
        RefreshReminders();

        if (_repository.LoadErrors.Count > 0)
            _dialogs.ShowError("Néhány jegyzet nem olvasható", string.Join("\n", _repository.LoadErrors));

        SelectedNote = FindLastOpened() ?? FirstVisible();
    }

    // ───────────────────────── note commands ─────────────────────────

    [RelayCommand]
    public void NewNote()
    {
        var item = new NoteItemViewModel(new Note());
        _repository.Save(item.Model);
        Notes.Add(item);

        SearchText = string.Empty;
        SelectedNote = item;
        NoteCreated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Selects a note by id (e.g. requested by openCalendar). Returns <c>false</c> if it doesn't exist.</summary>
    public bool OpenNoteById(Guid id)
    {
        var target = Notes.FirstOrDefault(n => n.Id == id);
        if (target is null) return false;

        SearchText = string.Empty;
        SelectedNote = target;
        return true;
    }

    /// <summary>Selects the last opened note (or the newest), creating one if the library is empty.</summary>
    public void OpenLastNote()
    {
        var target = FindLastOpened() ?? Notes.OrderByDescending(n => n.UpdatedAt).FirstOrDefault();
        if (target is null)
        {
            NewNote();
            return;
        }

        if (!target.Matches(SearchText)) SearchText = string.Empty;
        SelectedNote = target;
    }

    [RelayCommand]
    private void DeleteNote(NoteItemViewModel? item)
    {
        item ??= SelectedNote;
        if (item is null) return;

        if (!_dialogs.Confirm("Jegyzet törlése",
                $"Biztosan törlöd a(z) „{item.Title}” jegyzetet?\nA fájl a Kuka mappába kerül, onnan visszaállítható.",
                "Törlés", destructive: true))
            return;

        if (ReferenceEquals(SelectedNote, item))
        {
            var visible = NotesView.Cast<NoteItemViewModel>().ToList();
            var index = visible.IndexOf(item);
            SelectedNote = visible.ElementAtOrDefault(index + 1) ?? visible.ElementAtOrDefault(index - 1);
        }

        TryRun("Törlési hiba", () => _repository.Delete(item.Id));
        Notes.Remove(item);
    }

    [RelayCommand]
    private void TogglePin(NoteItemViewModel? item)
    {
        item ??= SelectedNote;
        if (item is null) return;

        if (ReferenceEquals(item, SelectedNote)) Editor.Flush();
        item.IsPinned = !item.IsPinned;
        TryRun("Mentési hiba", () => _repository.Save(item.Model));
        if (ReferenceEquals(item, SelectedNote)) Editor.NotifyPinChanged();
    }

    [RelayCommand]
    private void ExportOpenNote(NoteItemViewModel? item) => Export(item, NoteExportFormat.OpenNote);

    [RelayCommand]
    private void ExportMarkdown(NoteItemViewModel? item) => Export(item, NoteExportFormat.Markdown);

    [RelayCommand]
    private void Import()
    {
        var files = _dialogs.PickFilesToOpen("Jegyzetek importálása", _importService.DialogFilter);
        if (files.Count > 0) ImportFiles(files);
    }

    /// <summary>Imports .openNote / .md files and selects the last one. Used by dialogs, drag &amp; drop and file association.</summary>
    [RelayCommand]
    public void ImportFiles(IReadOnlyList<string>? paths)
    {
        if (paths is null || paths.Count == 0) return;

        var errors = new List<string>();
        NoteItemViewModel? last = null;

        foreach (var path in paths)
        {
            try
            {
                if (!_importService.CanImport(path))
                {
                    errors.Add($"{Path.GetFileName(path)}: nem támogatott fájltípus");
                    continue;
                }

                last = AddImported(_importService.Import(path)) ?? last;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
                                           or NotSupportedException or System.Text.Json.JsonException)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        if (last is not null)
        {
            SearchText = string.Empty;
            SelectedNote = last;
        }

        if (errors.Count > 0) _dialogs.ShowError("Importálási hiba", string.Join("\n", errors));
    }

    // ───────────────────────── app commands ─────────────────────────

    [RelayCommand]
    private void SaveNow() => Editor.SaveNowCommand.Execute(null);

    /// <summary>Opens an embedded image (parameter: Markdown path such as <c>assets/x.png</c>) in the viewer.</summary>
    [RelayCommand]
    private void OpenImage(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || Editor.GetAsset(relativePath) is not { } data) return;
        _windows.ShowImage(data, Path.GetFileName(relativePath));
    }

    [RelayCommand]
    private void OpenSettings() => _windows.ShowSettings();

    [RelayCommand]
    private void ToggleMiniMode() => _windows.ToggleMiniMode();

    [RelayCommand]
    private void ShowMain() => _windows.ShowMain();

    [RelayCommand]
    private void CycleViewMode() => ViewMode = ViewMode switch
    {
        ViewMode.Editor => ViewMode.Split,
        ViewMode.Split => ViewMode.Preview,
        _ => ViewMode.Editor,
    };

    [RelayCommand]
    private void OpenNotesFolder() =>
        Process.Start(new ProcessStartInfo(_paths.NotesDirectory) { UseShellExecute = true });

    // ───────────────────────── state ─────────────────────────

    partial void OnSelectedNoteChanged(NoteItemViewModel? value)
    {
        Editor.Load(value);
        if (value is null) return;
        _settings.Current.LastOpenedNoteId = value.Id;
    }

    partial void OnSearchTextChanged(string value)
    {
        NotesView.Refresh();
        UpdateListState();
    }

    partial void OnViewModeChanged(ViewMode value) => _settings.Current.ViewMode = value;

    private void ApplySettings()
    {
        var settings = _settings.Current;
        ViewMode = settings.ViewMode;
        EditorFontSize = settings.EditorFontSize;
        QuickNoteHotkeyText = $"Gyors jegyzet: {settings.NewNoteHotkey}   ·   Utolsó jegyzet: {settings.OpenLastNoteHotkey}";
    }

    private void UpdateListState()
    {
        // Computed from the source collection: when CollectionChanged fires, the view may not have
        // processed the change yet, so NotesView.IsEmpty can be stale.
        IsListEmpty = !Notes.Any(n => n.Matches(SearchText));
        OnPropertyChanged(nameof(NoteCountText));
        OnPropertyChanged(nameof(EmptyListText));
    }

    private NoteItemViewModel? AddImported(Note imported)
    {
        var existing = Notes.FirstOrDefault(n => n.Id == imported.Id);
        if (existing is not null)
        {
            var choice = _dialogs.Ask("A jegyzet már létezik",
                $"A(z) „{existing.Title}” jegyzet már szerepel a könyvtáradban.\nFelülírod az importált változattal, vagy külön másolatként adod hozzá?",
                "Felülírás", "Másolat");

            switch (choice)
            {
                case DialogChoice.Cancel:
                    return null;
                case DialogChoice.Primary:
                    _repository.Save(imported);
                    var index = Notes.IndexOf(existing);
                    var replacement = new NoteItemViewModel(imported);
                    if (ReferenceEquals(SelectedNote, existing)) SelectedNote = null;
                    Notes[index] = replacement;
                    return replacement;
                default:
                    imported.Id = Guid.NewGuid();
                    imported.Title = $"{imported.DisplayTitle} (másolat)";
                    break;
            }
        }

        _repository.Save(imported);
        var item = new NoteItemViewModel(imported);
        Notes.Add(item);
        return item;
    }

    private void Export(NoteItemViewModel? item, NoteExportFormat format)
    {
        item ??= SelectedNote;
        if (item is null) return;
        if (ReferenceEquals(item, SelectedNote)) Editor.Flush();

        var exporter = _exporters.First(e => e.Format == format);
        var filter = $"{exporter.DisplayName} (*{exporter.Extension})|*{exporter.Extension}";
        var path = _dialogs.PickSaveFile("Jegyzet exportálása", filter, SafeFileName(item.Title) + exporter.Extension);
        if (path is null) return;

        TryRun("Exportálási hiba", () => exporter.Export(item.Model, path));
    }

    private void TryRun(string errorTitle, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dialogs.ShowError(errorTitle, ex.Message);
        }
    }

    private NoteItemViewModel? FindLastOpened() =>
        _settings.Current.LastOpenedNoteId is { } id ? Notes.FirstOrDefault(n => n.Id == id) : null;

    private NoteItemViewModel? FirstVisible() => NotesView.Cast<NoteItemViewModel>().FirstOrDefault();

    private static string SafeFileName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(title.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return clean.Length == 0 ? "jegyzet" : clean;
    }
}
