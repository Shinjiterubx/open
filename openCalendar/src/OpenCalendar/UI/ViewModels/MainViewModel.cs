using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCalendar.Core.Abstractions;
using OpenCalendar.Core.Agenda;
using OpenCalendar.Core.Models;
using OpenCalendar.Core.Reminders;
using OpenCalendar.Core.Time;
using OpenCalendar.Infrastructure.Storage;
using OpenCalendar.Services;
using OpenCalendar.UI.Services;

namespace OpenCalendar.UI.ViewModels;

/// <summary>The calendar shell: navigation, the four views, the calendar list and all event / calendar commands.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private const int AgendaRangeDays = 60;
    private const int UpcomingCount = 6;

    private readonly CalendarService _service;
    private readonly ISettingsStore _settings;
    private readonly IDialogService _dialogs;
    private readonly IWindowService _windows;
    private readonly INoteLauncher _noteLauncher;
    private readonly IKanbanLauncher _kanbanLauncher;
    private readonly IAppPaths _paths;
    private readonly DispatcherTimer _clock;
    private DateOnly _today = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty] private CalendarViewMode _viewMode;
    [ObservableProperty] private DateOnly _selectedDate = DateOnly.FromDateTime(DateTime.Today);
    [ObservableProperty] private string _periodTitle = string.Empty;
    [ObservableProperty] private int _dayColumnCount = 7;
    [ObservableProperty] private bool _isAgendaEmpty;

    public MainViewModel(CalendarService service, ISettingsStore settings, IDialogService dialogs, IWindowService windows,
        INoteLauncher noteLauncher, IKanbanLauncher kanbanLauncher, IAppPaths paths)
    {
        _service = service;
        _settings = settings;
        _dialogs = dialogs;
        _windows = windows;
        _noteLauncher = noteLauncher;
        _kanbanLauncher = kanbanLauncher;
        _paths = paths;
        _viewMode = settings.Current.ViewMode;

        MiniCalendar.DateSelected += (_, date) => SelectedDate = date;
        _service.Changed += (_, _) => Refresh();
        _settings.Saved += (_, _) => Refresh();

        // Keeps "now" line, "today" and the agenda current; also rolls over at midnight.
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _clock.Tick += (_, _) => OnClockTick();
        _clock.Start();
    }

    public MiniCalendarViewModel MiniCalendar { get; } = new();
    public ObservableCollection<CalendarListItemViewModel> Calendars { get; } = new();
    public ObservableCollection<MonthCellViewModel> MonthCells { get; } = new();
    public ObservableCollection<DayColumnViewModel> DayColumns { get; } = new();
    public ObservableCollection<AgendaDayViewModel> AgendaDays { get; } = new();
    public ObservableCollection<EntryViewModel> Upcoming { get; } = new();
    public IReadOnlyList<string> WeekdayHeaders => Hu.ShortDayNames;
    public IReadOnlyList<int> Hours { get; } = Enumerable.Range(0, 24).ToList();

    public bool HasUpcoming => Upcoming.Count > 0;

    // ── openNotes integration (sidebar row) ──
    public bool IsNotesSyncEnabled => _settings.Current.OpenNotesSyncEnabled;

    public bool ShowNotesCalendar
    {
        get => _settings.Current.OpenNotesCalendarVisible;
        set
        {
            if (_settings.Current.OpenNotesCalendarVisible == value) return;
            _settings.Current.OpenNotesCalendarVisible = value;
            TrySaveSettings();   // Saved → Refresh()
            OnPropertyChanged();
        }
    }

    // ── openKanban integration (sidebar row) ──
    public bool IsKanbanSyncEnabled => _settings.Current.KanbanSyncEnabled && _service.IsKanbanAvailable;

    public bool ShowKanbanCalendar
    {
        get => _settings.Current.KanbanCalendarVisible;
        set
        {
            if (_settings.Current.KanbanCalendarVisible == value) return;
            _settings.Current.KanbanCalendarVisible = value;
            TrySaveSettings();   // Saved → Refresh()
            OnPropertyChanged();
        }
    }

    public string KanbanStatusText => $"{_service.KanbanCards.Count(c => !c.IsDone)} nyitott határidős kártya";

    public string NotesStatusText => !IsNotesSyncEnabled
        ? "Szinkron kikapcsolva"
        : _service.NotesFolder is null
            ? "Az openNotes nem található"
            : $"{_service.Notes.Count(n => n.DueDate is not null && !n.IsDone)} aktív határidő";

    // ── work hours shading in the time grid (pixels at 48 px / hour) ──
    public double WorkdayTop => _settings.Current.WorkdayStart.ToTimeSpan().TotalMinutes * TimeGrid.PixelsPerMinute;
    public double WorkdayHeight => (_settings.Current.WorkdayEnd - _settings.Current.WorkdayStart).TotalMinutes * TimeGrid.PixelsPerMinute;
    public double InitialScrollOffset => Math.Max(0, (_settings.Current.WorkdayStart.ToTimeSpan().TotalMinutes - 30) * TimeGrid.PixelsPerMinute);

    /// <summary>Raised after a view was rebuilt (views use it to scroll to the working hours).</summary>
    public event EventHandler? ViewRebuilt;

    public void Initialize()
    {
        _service.Load();
        if (_service.LoadErrors.Count > 0)
            _dialogs.ShowError("Néhány naptár nem olvasható", string.Join("\n", _service.LoadErrors));
    }

    // ───────────────────────── navigation ─────────────────────────

    /// <summary>Shows a given day (used by <c>--goto yyyy-MM-dd</c>, e.g. from openKanban).</summary>
    public void GoToDate(DateOnly date)
    {
        SelectedDate = date;
        if (ViewMode == CalendarViewMode.Agenda) ViewMode = CalendarViewMode.Week;
    }

    [RelayCommand]
    private void GoToday() => SelectedDate = DateOnly.FromDateTime(DateTime.Today);

    [RelayCommand]
    private void Previous() => SelectedDate = Step(-1);

    [RelayCommand]
    private void Next() => SelectedDate = Step(1);

    [RelayCommand]
    private void ShowDay(object? parameter)
    {
        if (parameter is DateOnly date) SelectedDate = date;
        else if (parameter is MonthCellViewModel cell) SelectedDate = cell.Date;
        else if (parameter is DayColumnViewModel column) SelectedDate = column.Date;
        ViewMode = CalendarViewMode.Day;
    }

    [RelayCommand]
    private void SetView(CalendarViewMode mode) => ViewMode = mode;

    private DateOnly Step(int direction) => ViewMode switch
    {
        CalendarViewMode.Day => SelectedDate.AddDays(direction),
        CalendarViewMode.Week => SelectedDate.AddDays(7 * direction),
        CalendarViewMode.Month => SelectedDate.AddMonths(direction),
        _ => SelectedDate.AddDays(14 * direction),
    };

    partial void OnSelectedDateChanged(DateOnly value) => Refresh();

    partial void OnViewModeChanged(CalendarViewMode value)
    {
        _settings.Current.ViewMode = value;
        Refresh();
    }

    // ───────────────────────── events ─────────────────────────

    [RelayCommand]
    private void NewEvent()
    {
        var now = DateTime.Now;
        var start = SelectedDate == _today
            ? now.Date.AddHours(now.Hour + 1)
            : SelectedDate.ToDateTime(new TimeOnly(9, 0));
        CreateEventAt(start, allDay: false);
    }

    /// <summary>Opens the editor for a new event (double-click on the grid / month cell).</summary>
    public void CreateEventAt(DateTime start, bool allDay)
    {
        var calendar = _service.Calendars.FirstOrDefault(c => c.IsVisible) ?? _service.Calendars.FirstOrDefault();
        if (calendar is null) return;

        var duration = TimeSpan.FromMinutes(_settings.Current.DefaultDurationMinutes);
        var ev = new CalendarEvent
        {
            Start = start,
            End = allDay ? start.Date : start + duration,
            IsAllDay = allDay,
            ReminderMinutes = new List<int>(_settings.Current.DefaultReminderMinutes),
        };
        OpenEditor(calendar.Id, ev, isNew: true, occurrenceDate: null);
    }

    [RelayCommand]
    private void OpenEntry(EntryViewModel? entry)
    {
        if (entry is null) return;

        if (entry.Entry.Kind == EntryKind.KanbanCard)
        {
            var cardId = Guid.ParseExact(entry.Entry.Key["kanban:".Length..], "N");
            if (!_kanbanLauncher.TryOpenCard(cardId))
                _dialogs.ShowError("openKanban nem található", "A kártya megnyitásához telepítsd az openKanban alkalmazást, és indítsd el legalább egyszer.");
            return;
        }

        if (entry.Entry.Kind == EntryKind.NoteDeadline)
        {
            OpenNote(entry.Entry.LinkedNoteId);
            return;
        }

        if (entry.Entry.Occurrence is { } occurrence)
            OpenEvent(occurrence.Event.Id, DateOnly.FromDateTime(occurrence.Start));
    }

    [RelayCommand]
    private void OpenLinkedNote(EntryViewModel? entry) => OpenNote(entry?.Entry.LinkedNoteId);

    /// <summary>Opens an event in the editor (also used by reminder notifications).</summary>
    public void OpenEvent(Guid eventId, DateOnly? occurrenceDate)
    {
        if (_service.FindEvent(eventId) is not { } found)
        {
            _dialogs.ShowError("Esemény nem található", "Az esemény időközben törlődött.");
            return;
        }
        OpenEditor(found.Calendar.Id, found.Event, isNew: false, found.Event.IsRecurring ? occurrenceDate : null);
    }

    public void OpenNote(Guid? noteId)
    {
        if (noteId is not { } id) return;
        if (!_noteLauncher.TryOpen(id))
            _dialogs.ShowError("openNotes nem található",
                "A jegyzet megnyitásához telepítsd az openNotes alkalmazást, és indítsd el legalább egyszer.");
    }

    private void OpenEditor(Guid calendarId, CalendarEvent ev, bool isNew, DateOnly? occurrenceDate)
    {
        var editor = new EventEditorViewModel(_service.Calendars, calendarId, isNew ? ev : ev.Clone(), isNew, occurrenceDate,
            _service.Notes, _dialogs, _noteLauncher);

        if (!_windows.ShowEventEditor(editor)) return;

        TryRun("Mentési hiba", () =>
        {
            switch (editor.Outcome)
            {
                case EditorOutcome.Saved when editor.Result is not null:
                    _service.SaveEvent(editor.ResultCalendarId, editor.Result);
                    var startDate = DateOnly.FromDateTime(editor.Result.Start);
                    if (!IsVisibleInView(startDate)) SelectedDate = startDate;   // jump to the saved event
                    break;
                case EditorOutcome.Deleted:
                    _service.DeleteEvent(ev.Id);
                    break;
                case EditorOutcome.OccurrenceDeleted when editor.OccurrenceDate is { } date:
                    _service.DeleteOccurrence(ev.Id, date);
                    break;
            }
        });
    }

    // ───────────────────────── calendars ─────────────────────────

    [RelayCommand]
    private void NewCalendar()
    {
        var color = CalendarDocument.Palette[_service.Calendars.Count % CalendarDocument.Palette.Length];
        var dialog = new CalendarDialogViewModel("Új naptár", string.Empty, color);
        if (_windows.ShowCalendarDialog(dialog))
            TryRun("Mentési hiba", () => _service.AddCalendar(dialog.Name, dialog.Color));
    }

    [RelayCommand]
    private void EditCalendar(CalendarListItemViewModel? item)
    {
        if (item is null) return;
        var dialog = new CalendarDialogViewModel("Naptár szerkesztése", item.Name, item.Color);
        if (_windows.ShowCalendarDialog(dialog))
            TryRun("Mentési hiba", () => _service.UpdateCalendar(item.Document, dialog.Name, dialog.Color));
    }

    [RelayCommand]
    private void DeleteCalendar(CalendarListItemViewModel? item)
    {
        if (item is null) return;
        if (_service.Calendars.Count <= 1)
        {
            _dialogs.ShowError("Nem törölhető", "Legalább egy naptárnak maradnia kell.");
            return;
        }
        if (!_dialogs.Confirm("Naptár törlése",
                $"Biztosan törlöd a(z) „{item.Name}” naptárt és {item.EventCount} eseményét?\nA fájl a Kuka mappába kerül, onnan visszaállítható.",
                "Törlés", destructive: true))
            return;

        TryRun("Törlési hiba", () => _service.DeleteCalendar(item.Document));
    }

    [RelayCommand]
    private void ExportCalendar(CalendarListItemViewModel? item)
    {
        if (item is null) return;
        Export(new[] { item.Document }, SafeFileName(item.Name));
    }

    [RelayCommand]
    private void ExportAll() => Export(_service.Calendars.ToList(), $"openCalendar-{DateTime.Today:yyyy-MM-dd}");

    [RelayCommand]
    private void Import()
    {
        var files = _dialogs.PickFilesToOpen("Naptár importálása", $"openCalendar naptár (*{OpenCalendarSerializer.FileExtension})|*{OpenCalendarSerializer.FileExtension}");
        if (files.Count > 0) ImportFiles(files);
    }

    /// <summary>Imports .opencalendar files (dialog, drag &amp; drop, file association).</summary>
    public void ImportFiles(IEnumerable<string> paths)
    {
        var errors = new List<string>();
        var imported = 0;

        foreach (var path in paths.Where(p => p.EndsWith(OpenCalendarSerializer.FileExtension, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                foreach (var calendar in _service.ReadFile(path))
                {
                    var replace = false;
                    if (_service.FindCalendar(calendar.Id) is { } existing)
                    {
                        var choice = _dialogs.Ask("A naptár már létezik",
                            $"A(z) „{existing.Name}” naptár már szerepel. Felülírod az importált változattal, vagy külön naptárként adod hozzá?",
                            "Felülírás", "Külön naptárként");
                        if (choice == DialogChoice.Cancel) continue;
                        replace = choice == DialogChoice.Primary;
                    }
                    _service.Import(calendar, replace);
                    imported++;
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        if (errors.Count > 0) _dialogs.ShowError("Importálási hiba", string.Join("\n", errors));
        else if (imported == 0) _dialogs.ShowError("Nincs mit importálni", "Válassz .opencalendar fájlt.");
    }

    [RelayCommand]
    private void OpenSettings() => _windows.ShowSettings();

    [RelayCommand]
    private void OpenDataFolder() => Process.Start(new ProcessStartInfo(_paths.CalendarsDirectory) { UseShellExecute = true });

    // ───────────────────────── view building ─────────────────────────

    /// <summary>Rebuilds the visible view, the sidebar lists and the mini calendar from the current data.</summary>
    public void Refresh()
    {
        _today = DateOnly.FromDateTime(DateTime.Today);
        SyncCalendarList();

        switch (ViewMode)
        {
            case CalendarViewMode.Month: BuildMonth(); break;
            case CalendarViewMode.Week: BuildDays(WeekMath.StartOfWeek(SelectedDate), 7); break;
            case CalendarViewMode.Day: BuildDays(SelectedDate, 1); break;
            default: BuildAgenda(); break;
        }

        PeriodTitle = BuildPeriodTitle();
        MiniCalendar.Update(SelectedDate, DatesWithEntries);
        BuildUpcoming();
        OnPropertyChanged(nameof(NotesStatusText));
        OnPropertyChanged(nameof(KanbanStatusText));
        OnPropertyChanged(nameof(IsKanbanSyncEnabled));
        OnPropertyChanged(nameof(IsNotesSyncEnabled));
        OnPropertyChanged(nameof(WorkdayTop));
        OnPropertyChanged(nameof(WorkdayHeight));
        ViewRebuilt?.Invoke(this, EventArgs.Empty);
    }

    private void BuildMonth()
    {
        var first = WeekMath.MonthGridStart(SelectedDate.Year, SelectedDate.Month);
        var entries = Query(first, first.AddDays(42));

        MonthCells.Clear();
        for (var i = 0; i < 42; i++)
        {
            var date = first.AddDays(i);
            var dayEntries = entries.Where(e => e.IsOnDay(date)).Select(e => new EntryViewModel(e)).ToList();
            MonthCells.Add(new MonthCellViewModel(date, date.Month == SelectedDate.Month, date == _today, date == SelectedDate, dayEntries));
        }
    }

    private void BuildDays(DateOnly first, int count)
    {
        var entries = Query(first, first.AddDays(count));
        var nowMinute = DateTime.Now.TimeOfDay.TotalMinutes;

        DayColumns.Clear();
        for (var i = 0; i < count; i++)
        {
            var date = first.AddDays(i);
            var dayEntries = entries.Where(e => e.IsOnDay(date)).ToList();
            var allDay = dayEntries.Where(IsAllDayLike).Select(e => new EntryViewModel(e)).ToList();
            var timed = dayEntries.Where(e => !IsAllDayLike(e)).Select(e => new EntryViewModel(e, date)).ToList();

            var slots = DayLayout.Arrange(timed.Select(t => (t.StartMinute, t.EndMinute)).ToList());
            for (var j = 0; j < timed.Count; j++)
            {
                timed[j].Column = slots[j].Column;
                timed[j].ColumnCount = slots[j].ColumnCount;
            }

            DayColumns.Add(new DayColumnViewModel(date, date == _today, allDay, timed) { NowMinute = nowMinute });
        }
        DayColumnCount = count;
    }

    /// <summary>Timed events spanning more than a day are shown in the all-day row (like other calendars).</summary>
    private static bool IsAllDayLike(CalendarEntry entry) => entry.IsAllDay || entry.End - entry.Start >= TimeSpan.FromHours(24);

    private void BuildAgenda()
    {
        var entries = Query(SelectedDate, SelectedDate.AddDays(AgendaRangeDays));
        AgendaDays.Clear();
        for (var i = 0; i < AgendaRangeDays; i++)
        {
            var date = SelectedDate.AddDays(i);
            var dayEntries = entries.Where(e => e.IsOnDay(date)).Select(e => new EntryViewModel(e)).ToList();
            if (dayEntries.Count > 0) AgendaDays.Add(new AgendaDayViewModel(date, _today, dayEntries));
        }
        IsAgendaEmpty = AgendaDays.Count == 0;
    }

    private void BuildUpcoming()
    {
        var now = DateTime.Now;
        var items = _service.Query(now.Date, now.Date.AddDays(14))
            .Where(e => e.EffectiveEnd > now && !e.IsDone)
            .Take(UpcomingCount)
            .Select(e => new EntryViewModel(e))
            .ToList();

        Upcoming.Clear();
        foreach (var item in items) Upcoming.Add(item);
        OnPropertyChanged(nameof(HasUpcoming));
    }

    private void SyncCalendarList()
    {
        var current = _service.Calendars;
        if (Calendars.Select(c => c.Document).SequenceEqual(current))
        {
            // Same calendars: names / colours may have changed – rebuild the items cheaply.
            if (Calendars.Zip(current).All(p => p.First.Name == p.Second.Name && p.First.Color == p.Second.Color)) return;
        }

        Calendars.Clear();
        foreach (var calendar in current) Calendars.Add(new CalendarListItemViewModel(calendar, _service));
    }

    private IReadOnlyList<CalendarEntry> Query(DateOnly from, DateOnly to) =>
        _service.Query(from.ToDateTime(TimeOnly.MinValue), to.ToDateTime(TimeOnly.MinValue));

    private ISet<DateOnly> DatesWithEntries(DateOnly from, DateOnly to)
    {
        var set = new HashSet<DateOnly>();
        foreach (var entry in Query(from, to))
        {
            var day = DateOnly.FromDateTime(entry.Start);
            var last = DateOnly.FromDateTime(entry.EffectiveEnd.AddTicks(-1));
            for (; day <= last && day < to; day = day.AddDays(1)) set.Add(day);
        }
        return set;
    }

    private string BuildPeriodTitle()
    {
        var c = Hu.Culture;
        switch (ViewMode)
        {
            case CalendarViewMode.Month:
                return Hu.Capitalize(SelectedDate.ToString("yyyy. MMMM", c));
            case CalendarViewMode.Day:
                return Hu.Capitalize(SelectedDate.ToString("yyyy. MMMM d., dddd", c));
            case CalendarViewMode.Week:
                var start = WeekMath.StartOfWeek(SelectedDate);
                var end = start.AddDays(6);
                var week = System.Globalization.ISOWeek.GetWeekOfYear(start.ToDateTime(TimeOnly.MinValue));
                var range = start.Month == end.Month
                    ? $"{start.ToString("yyyy. MMMM d.", c)} – {end.Day}."
                    : start.Year == end.Year
                        ? $"{start.ToString("yyyy. MMM d.", c)} – {end.ToString("MMM d.", c)}"
                        : $"{start.ToString("yyyy. MMM d.", c)} – {end.ToString("yyyy. MMM d.", c)}";
                return $"{Hu.Capitalize(range)}  ·  {week}. hét";
            default:
                return $"Napló  ·  {SelectedDate.ToString("yyyy. MMM d.", c)}-tól";
        }
    }

    private bool IsVisibleInView(DateOnly date) => ViewMode switch
    {
        CalendarViewMode.Day => date == SelectedDate,
        CalendarViewMode.Week => WeekMath.StartOfWeek(date) == WeekMath.StartOfWeek(SelectedDate),
        CalendarViewMode.Month => date.Year == SelectedDate.Year && date.Month == SelectedDate.Month,
        _ => date >= SelectedDate && date < SelectedDate.AddDays(AgendaRangeDays),
    };

    private void OnClockTick()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        if (today != _today)
        {
            Refresh();
            return;
        }

        var minute = DateTime.Now.TimeOfDay.TotalMinutes;
        foreach (var column in DayColumns) column.NowMinute = minute;
        BuildUpcoming();
    }

    private void Export(IReadOnlyCollection<CalendarDocument> calendars, string defaultName)
    {
        var ext = OpenCalendarSerializer.FileExtension;
        var path = _dialogs.PickSaveFile("Naptár exportálása", $"openCalendar naptár (*{ext})|*{ext}", defaultName + ext);
        if (path is null) return;
        TryRun("Exportálási hiba", () => _service.Export(calendars, path));
    }

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

    private void TrySaveSettings()
    {
        try { _settings.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* saved again on exit */ }
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return clean.Length == 0 ? "naptar" : clean;
    }
}

/// <summary>Geometry of the hour grid, shared by view models and views.</summary>
public static class TimeGrid
{
    public const double HourHeight = 48;
    public const double PixelsPerMinute = HourHeight / 60;
    public const double TotalHeight = HourHeight * 24;
}
