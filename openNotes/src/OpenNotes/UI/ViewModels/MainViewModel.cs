using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Library;
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
    private readonly IFolderRepository _folderRepository;
    private readonly DispatcherTimer _bannerTimer;

    [ObservableProperty] private NoteItemViewModel? _selectedNote;
    [ObservableProperty] private FolderNavItemViewModel? _selectedNav;
    [ObservableProperty] private bool _showClosed;
    [ObservableProperty] private bool _isCardView;
    [ObservableProperty] private string? _bannerText;
    [ObservableProperty] private string? _bannerActionText;
    [ObservableProperty] private int _closedCount;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private ViewMode _viewMode;
    [ObservableProperty] private double _editorFontSize;
    [ObservableProperty] private bool _isListEmpty;
    [ObservableProperty] private string _quickNoteHotkeyText = string.Empty;
    [ObservableProperty] private bool _isReminderPanelExpanded = true;
    [ObservableProperty] private bool _isFolderPanelExpanded = true;
    [ObservableProperty] private ReminderCounts _reminderCounts = new(0, 0, 0);

    private string _reminderSignature = string.Empty;
    private bool _isLoading;
    private Action? _bannerAction;

    /// <summary>
    /// The open note keeps its place in the list after it was closed or moved out of the current folder,
    /// until another note is selected – so it doesn't vanish from under the cursor.
    /// </summary>
    private NoteItemViewModel? _sticky;

    public MainViewModel(
        INoteRepository repository,
        NoteImportService importService,
        IEnumerable<INoteExporter> exporters,
        ISettingsStore settings,
        IDialogService dialogs,
        IWindowService windows,
        IAppPaths paths,
        IFolderRepository folderRepository,
        NoteEditorViewModel editor)
    {
        _folderRepository = folderRepository;
        _bannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _bannerTimer.Tick += (_, _) => DismissBanner();

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
        view.Filter = item => item is NoteItemViewModel note && IsListed(note);
        view.SortDescriptions.Add(new SortDescription(nameof(NoteItemViewModel.IsPinned), ListSortDirection.Descending));
        view.SortDescriptions.Add(new SortDescription(nameof(NoteItemViewModel.UpdatedAt), ListSortDirection.Descending));
        view.IsLiveSorting = true;
        view.LiveSortingProperties.Add(nameof(NoteItemViewModel.IsPinned));
        view.LiveSortingProperties.Add(nameof(NoteItemViewModel.UpdatedAt));
        NotesView = view;

        ApplySettings();
        _settings.Saved += (_, _) => ApplySettings();
        Notes.CollectionChanged += (_, _) => RefreshReminders();
        Editor.TaskStateChanged += (_, _) =>
        {
            RefreshReminders();
            OnSelectedNoteStateChanged();
        };
        IsReminderPanelExpanded = _settings.Current.ReminderPanelExpanded;
        IsFolderPanelExpanded = _settings.Current.FolderPanelExpanded;

        Editor.SaveFailed += (_, ex) => _dialogs.ShowError("Mentési hiba", $"A jegyzet nem menthető:\n{ex.Message}");
    }

    public ObservableCollection<NoteItemViewModel> Notes { get; }
    public ICollectionView NotesView { get; }
    public NoteEditorViewModel Editor { get; }
    public string NoteCountText => Notes.Count(n => n.IsDone == ShowClosed && CurrentScope.Includes(n.Model)).ToString();

    public string EmptyListText
    {
        get
        {
            if (Notes.Count == 0) return "Még nincs jegyzeted.\nKezdj egy újat!";
            if (!string.IsNullOrWhiteSpace(SearchText)) return "Nincs találat.";
            if (ShowClosed) return "Nincs lezárt jegyzet.\nA „Kész” jelölésű jegyzetek kerülnek ide.";
            return CurrentScope.Kind == NoteScopeKind.Folder
                ? "Ez a mappa üres.\nAz itt létrehozott jegyzetek ide kerülnek."
                : "Nincs nyitott jegyzet.";
        }
    }

    public string ListHeaderText
    {
        get
        {
            var folder = SelectedNav is { IsFolder: true } nav ? nav.Name : null;
            if (ShowClosed) return folder is null ? "Lezártak" : $"{folder} – lezártak";
            return folder ?? (SelectedNav?.Kind == NoteScopeKind.Unfiled ? "Mappa nélkül" : "Jegyzetek");
        }
    }

    public string ClosedSwitchText => ClosedCount > 0 ? $"Lezártak ({ClosedCount})" : "Lezártak";

    // ───────────────────────── folders ─────────────────────────

    /// <summary>Sidebar navigation: "Összes jegyzet", "Mappa nélkül", then the folders.</summary>
    public ObservableCollection<FolderNavItemViewModel> FolderNav { get; } = new();

    /// <summary>Targets of "Áthelyezés mappába": "Mappa nélkül" and the folders.</summary>
    public ObservableCollection<FolderNavItemViewModel> MoveTargets { get; } = new();

    /// <summary>Card view sections (rebuilt while the card view is open).</summary>
    public ObservableCollection<NoteCardGroupViewModel> CardGroups { get; } = new();

    public bool IsCardViewEmpty => CardGroups.Count == 0;

    public bool HasBanner => BannerText is not null;
    public bool HasBannerAction => BannerActionText is not null;

    private NoteScope CurrentScope => SelectedNav?.Scope ?? NoteScope.All;

    private IEnumerable<FolderNavItemViewModel> RealFolders => FolderNav.Where(f => f.IsFolder);

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

    partial void OnIsFolderPanelExpandedChanged(bool value) => _settings.Current.FolderPanelExpanded = value;

    [RelayCommand]
    private void ToggleFolderPanel() => IsFolderPanelExpanded = !IsFolderPanelExpanded;

    [RelayCommand]
    private void OpenReminder(ReminderItemViewModel? item)
    {
        if (item is null) return;
        IsCardView = false;
        Reveal(item.Note);
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

        SetClosed(item.Note, true);
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
            LoadFolders();
            foreach (var note in _repository.LoadAll())
            {
                var item = new NoteItemViewModel(note);
                if (note.FolderId is { } folderId) item.Folder = RealFolders.FirstOrDefault(f => f.FolderId == folderId);
                Notes.Add(item);
            }
            SelectedNav = FolderNav[0];
        }
        finally
        {
            _isLoading = false;
        }
        RefreshList();
        RefreshReminders();

        if (_repository.LoadErrors.Count > 0)
            _dialogs.ShowError("Néhány jegyzet nem olvasható", string.Join("\n", _repository.LoadErrors));

        // Closed notes stay hidden at start-up, even if one was open last time.
        SelectedNote = FindLastOpened() is { IsDone: false } last ? last : FirstVisible();
    }

    // ───────────────────────── note commands ─────────────────────────

    [RelayCommand]
    public void NewNote()
    {
        // New notes are open notes; inside a folder view they are filed into that folder.
        var item = new NoteItemViewModel(new Note()) { Folder = SelectedNav is { IsFolder: true } nav ? nav : null };
        _repository.Save(item.Model);
        Notes.Add(item);

        IsCardView = false;
        Reveal(item);
        NoteCreated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Selects a note by id (e.g. requested by openCalendar). Returns <c>false</c> if it doesn't exist.</summary>
    public bool OpenNoteById(Guid id)
    {
        var target = Notes.FirstOrDefault(n => n.Id == id);
        if (target is null) return false;

        IsCardView = false;
        Reveal(target);
        return true;
    }

    /// <summary>Selects the last opened note (or the newest), creating one if the library is empty.</summary>
    public void OpenLastNote()
    {
        var target = (FindLastOpened() is { IsDone: false } last ? last : null)
                     ?? Notes.Where(n => !n.IsDone).OrderByDescending(n => n.UpdatedAt).FirstOrDefault();
        if (target is null)
        {
            NewNote();
            return;
        }

        Reveal(target);
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
            IsCardView = false;
            Reveal(last);
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
        UpdateOpenFolderMarker();

        if (_sticky is not null && !ReferenceEquals(value, _sticky))
        {
            _sticky = null;
            // Not inside the ListBox's selection change: refresh once it has finished.
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(RefreshList, DispatcherPriority.Background);
        }

        if (value is null) return;
        _settings.Current.LastOpenedNoteId = value.Id;
    }

    partial void OnSearchTextChanged(string value) => RefreshList();

    partial void OnShowClosedChanged(bool value)
    {
        if (_isLoading) return;
        _sticky = null;
        DismissBanner();
        RefreshList();
        EnsureSelectionVisible();
    }

    partial void OnSelectedNavChanged(FolderNavItemViewModel? value)
    {
        if (_isLoading) return;
        _sticky = null;
        RefreshList();
        EnsureSelectionVisible();
    }

    partial void OnIsCardViewChanged(bool value)
    {
        if (value) Editor.Flush();
        RebuildCards();
    }

    partial void OnBannerTextChanged(string? value) => OnPropertyChanged(nameof(HasBanner));

    partial void OnBannerActionTextChanged(string? value) => OnPropertyChanged(nameof(HasBannerAction));

    partial void OnClosedCountChanged(int value) => OnPropertyChanged(nameof(ClosedSwitchText));

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
        if (_isLoading) return;
        // Computed from the source collection: when CollectionChanged fires, the view may not have
        // processed the change yet, so NotesView.IsEmpty can be stale.
        IsListEmpty = !Notes.Any(IsListed);
        ClosedCount = Notes.Count(n => n.IsDone);
        foreach (var nav in FolderNav)
            nav.Count = Notes.Count(n => n.IsDone == ShowClosed && nav.Scope.Includes(n.Model));

        OnPropertyChanged(nameof(NoteCountText));
        OnPropertyChanged(nameof(EmptyListText));
        OnPropertyChanged(nameof(ListHeaderText));
        RebuildCards();
    }

    private bool IsListed(NoteItemViewModel note) =>
        ReferenceEquals(note, _sticky) || NoteFilter.IsVisible(note.Model, ShowClosed, CurrentScope, SearchText);

    private void RefreshList()
    {
        NotesView.Refresh();
        UpdateListState();
    }

    /// <summary>After switching mode / folder: keep the open note if it is listed, otherwise open the first listed one.</summary>
    private void EnsureSelectionVisible()
    {
        if (SelectedNote is not null && IsListed(SelectedNote)) return;
        SelectedNote = FirstVisible();
    }

    /// <summary>Shows a note: switches to open/closed notes and to a folder view that contains it, then selects it.</summary>
    private void Reveal(NoteItemViewModel item)
    {
        _isLoading = true;   // one refresh for all changes below
        try
        {
            SearchText = string.Empty;
            ShowClosed = item.IsDone;
            if (!CurrentScope.Includes(item.Model)) SelectedNav = FolderNav[0];
        }
        finally
        {
            _isLoading = false;
        }

        _sticky = null;
        RefreshList();
        SelectedNote = item;
    }

    // ───────────────────────── closing / reopening ─────────────────────────

    [RelayCommand]
    private void ToggleClosed(NoteItemViewModel? item)
    {
        item ??= SelectedNote;
        if (item is not null) SetClosed(item, !item.IsDone);
    }

    private void SetClosed(NoteItemViewModel item, bool closed)
    {
        if (item.IsDone == closed) return;

        if (ReferenceEquals(item, SelectedNote))
        {
            Editor.IsDone = closed;   // through the editor: autosave + OnSelectedNoteStateChanged
            return;
        }

        item.Model.IsDone = closed;
        item.Model.UpdatedAt = DateTimeOffset.Now;
        item.Refresh();
        TryRun("Mentési hiba", () => _repository.Save(item.Model));
        RefreshReminders();
        RefreshList();
        AnnounceClosedChange(item);
    }

    /// <summary>The open note was closed / reopened in the editor (or its task state changed).</summary>
    private void OnSelectedNoteStateChanged()
    {
        var item = SelectedNote;
        if (item is null) return;

        var listed = NoteFilter.IsVisible(item.Model, ShowClosed, CurrentScope, SearchText);
        if (listed)
        {
            if (ReferenceEquals(_sticky, item)) _sticky = null;
            UpdateListState();
            return;
        }

        if (item.IsDone == ShowClosed) return;   // hidden for another reason (e.g. search) – nothing to announce
        _sticky = item;
        RefreshList();
        AnnounceClosedChange(item);
    }

    private void AnnounceClosedChange(NoteItemViewModel item)
    {
        if (item.IsDone)
        {
            ShowBanner($"„{item.Title}” lezárva – a Lezártak között találod.", "Visszavonás", () => SetClosed(item, false));
        }
        else
        {
            ShowBanner($"„{item.Title}” újranyitva – visszakerült a jegyzetek közé.", "Megnyitás", () => Reveal(item));
        }
    }

    // ───────────────────────── folders ─────────────────────────

    private void LoadFolders()
    {
        FolderNav.Clear();
        MoveTargets.Clear();

        var all = FolderNavItemViewModel.CreateAll();
        var unfiled = FolderNavItemViewModel.CreateUnfiled();
        FolderNav.Add(all);
        FolderNav.Add(unfiled);
        MoveTargets.Add(unfiled);

        foreach (var folder in _folderRepository.LoadAll())
        {
            var nav = FolderNavItemViewModel.ForFolder(folder);
            FolderNav.Add(nav);
            MoveTargets.Add(nav);
        }
    }

    private void SaveFolders() =>
        TryRun("Mentési hiba", () => _folderRepository.SaveAll(RealFolders.Select(f => f.Folder!)));

    [RelayCommand]
    private void NewFolder() => CreateFolder();

    private FolderNavItemViewModel? CreateFolder()
    {
        var result = _dialogs.EditFolder("Új mappa", string.Empty, NextFolderColor(), NoteFolder.Palette, "Létrehozás");
        if (result is null) return null;

        var name = result.Name.Trim();
        if (RealFolders.Any(f => string.Equals(f.Name, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            _dialogs.ShowError("Már van ilyen mappa", $"„{name}” nevű mappa már létezik.");
            return null;
        }

        var nav = AddFolder(name, result.Color);
        UpdateListState();
        return nav;
    }

    /// <summary>Creates a folder, then moves the note (default: the open note) into it.</summary>
    [RelayCommand]
    private void NewFolderAndMove(NoteItemViewModel? item)
    {
        item ??= SelectedNote;
        var nav = CreateFolder();
        if (nav is not null && item is not null) MoveToFolder(new MoveRequest(item, nav));
    }

    [RelayCommand]
    private void EditFolder(FolderNavItemViewModel? nav)
    {
        if (nav is not { IsFolder: true }) return;

        var result = _dialogs.EditFolder("Mappa szerkesztése", nav.Name, nav.Color, NoteFolder.Palette, "Mentés");
        if (result is null) return;

        var name = result.Name.Trim();
        if (RealFolders.Any(f => !ReferenceEquals(f, nav) && string.Equals(f.Name, name, StringComparison.CurrentCultureIgnoreCase)))
        {
            _dialogs.ShowError("Már van ilyen mappa", $"„{name}” nevű mappa már létezik.");
            return;
        }

        nav.Update(name, result.Color);
        foreach (var note in Notes.Where(n => ReferenceEquals(n.Folder, nav))) note.RefreshFolder();
        SaveFolders();
        UpdateListState();
    }

    [RelayCommand]
    private void DeleteFolder(FolderNavItemViewModel? nav)
    {
        if (nav is not { IsFolder: true }) return;

        var notes = Notes.Where(n => ReferenceEquals(n.Folder, nav)).ToList();
        var message = notes.Count == 0
            ? $"A(z) „{nav.Name}” mappa üres, törölhető."
            : $"A(z) „{nav.Name}” mappa törlődik. A benne lévő {notes.Count} jegyzet megmarad, a „Mappa nélkül” részbe kerül.";
        if (!_dialogs.Confirm("Mappa törlése", message, "Mappa törlése", destructive: true)) return;

        if (SelectedNote is not null && notes.Contains(SelectedNote)) Editor.Flush();
        foreach (var note in notes)
        {
            note.Folder = null;
            TryRun("Mentési hiba", () => _repository.Save(note.Model));
        }

        if (ReferenceEquals(SelectedNav, nav)) SelectedNav = FolderNav[0];
        FolderNav.Remove(nav);
        MoveTargets.Remove(nav);
        SaveFolders();
        UpdateOpenFolderMarker();
        RefreshList();
    }

    /// <summary>Files a note into a folder ("Mappa nélkül" = take it out of its folder).</summary>
    [RelayCommand]
    private void MoveToFolder(MoveRequest? request)
    {
        if (request is null) return;
        var (item, target) = request;
        if (target.Kind == NoteScopeKind.All) return;

        var newFolder = target.IsFolder ? target : null;
        if (ReferenceEquals(item.Folder, newFolder)) return;

        var isOpenNote = ReferenceEquals(item, SelectedNote);
        if (isOpenNote) Editor.Flush();

        item.Folder = newFolder;
        TryRun("Mentési hiba", () => _repository.Save(item.Model));

        if (isOpenNote && !CurrentScope.Includes(item.Model))
        {
            // Follow the open note into its new folder, so it is obvious where it is now.
            _isLoading = true;
            SelectedNav = target;
            _isLoading = false;
        }

        RefreshList();
        if (isOpenNote) SelectedNote = item;
        UpdateOpenFolderMarker();
        ShowBanner(newFolder is null
            ? $"„{item.Title}” kivéve a mappából."
            : $"„{item.Title}” áthelyezve: {newFolder.Name}", null, null);
    }

    private FolderNavItemViewModel AddFolder(string name, string color)
    {
        var nav = FolderNavItemViewModel.ForFolder(new NoteFolder { Name = name, Color = color });
        FolderNav.Add(nav);
        MoveTargets.Add(nav);
        SaveFolders();
        return nav;
    }

    private string NextFolderColor() => NoteFolder.Palette[RealFolders.Count() % NoteFolder.Palette.Count];

    /// <summary>Imported notes: same folder id if it exists here, otherwise a folder with the same name (created if needed).</summary>
    private FolderNavItemViewModel? ResolveImportedFolder(Note note)
    {
        if (note.FolderId is { } id && RealFolders.FirstOrDefault(f => f.FolderId == id) is { } byId) return byId;
        if (string.IsNullOrWhiteSpace(note.FolderName)) return null;

        var name = note.FolderName.Trim();
        return RealFolders.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.CurrentCultureIgnoreCase))
               ?? AddFolder(name, NextFolderColor());
    }

    private void UpdateOpenFolderMarker()
    {
        var folderId = SelectedNote?.Model.FolderId;
        foreach (var nav in FolderNav) nav.ContainsOpenNote = nav.IsFolder && folderId is not null && nav.FolderId == folderId;
    }

    // ───────────────────────── card view ─────────────────────────

    [RelayCommand]
    private void ToggleCardView() => IsCardView = !IsCardView;

    [RelayCommand]
    private void OpenCard(NoteItemViewModel? item)
    {
        if (item is null) return;
        IsCardView = false;
        Reveal(item);
    }

    [RelayCommand]
    private void SelectNav(FolderNavItemViewModel? nav)
    {
        if (nav is not null) SelectedNav = nav;
    }

    private void RebuildCards()
    {
        if (!IsCardView)
        {
            if (CardGroups.Count > 0) CardGroups.Clear();
            OnPropertyChanged(nameof(IsCardViewEmpty));
            return;
        }

        var visible = Notes
            .Where(n => NoteFilter.IsVisible(n.Model, ShowClosed, CurrentScope, SearchText))
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAt)
            .ToList();

        CardGroups.Clear();
        foreach (var folder in RealFolders)
        {
            var notes = visible.Where(n => ReferenceEquals(n.Folder, folder)).ToList();
            if (notes.Count > 0) CardGroups.Add(new NoteCardGroupViewModel(folder.Name, folder.Color, true, notes));
        }

        var unfiled = visible.Where(n => n.Folder is null).ToList();
        if (unfiled.Count > 0) CardGroups.Add(new NoteCardGroupViewModel("Mappa nélkül", "#B9B6C4", false, unfiled));
        OnPropertyChanged(nameof(IsCardViewEmpty));
    }

    // ───────────────────────── banner ─────────────────────────

    private void ShowBanner(string text, string? actionText, Action? action)
    {
        _bannerAction = action;
        BannerActionText = action is null ? null : actionText;
        BannerText = text;
        _bannerTimer.Stop();
        _bannerTimer.Start();
    }

    [RelayCommand]
    private void BannerAction()
    {
        var action = _bannerAction;
        DismissBanner();
        action?.Invoke();
    }

    [RelayCommand]
    private void DismissBanner()
    {
        _bannerTimer.Stop();
        _bannerAction = null;
        BannerText = null;
        BannerActionText = null;
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
                    var replacement = new NoteItemViewModel(imported) { Folder = ResolveImportedFolder(imported) };
                    _repository.Save(imported);
                    var index = Notes.IndexOf(existing);
                    if (ReferenceEquals(SelectedNote, existing)) SelectedNote = null;
                    Notes[index] = replacement;
                    return replacement;
                default:
                    imported.Id = Guid.NewGuid();
                    imported.Title = $"{imported.DisplayTitle} (másolat)";
                    break;
            }
        }

        var item = new NoteItemViewModel(imported) { Folder = ResolveImportedFolder(imported) };
        _repository.Save(imported);
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
