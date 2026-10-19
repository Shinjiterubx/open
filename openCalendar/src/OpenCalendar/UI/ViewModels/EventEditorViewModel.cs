using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCalendar.Core.Abstractions;
using OpenCalendar.Core.Models;
using OpenCalendar.Core.Reminders;
using OpenCalendar.Core.Time;
using OpenCalendar.UI.Services;

namespace OpenCalendar.UI.ViewModels;

public enum EditorOutcome
{
    Cancelled,
    Saved,
    Deleted,
    OccurrenceDeleted,
}

public sealed record CalendarChoice(Guid Id, string Name, string Color);

public sealed record FrequencyChoice(RecurrenceFrequency Value, string Label);

public sealed record NoteChoice(Guid? Id, string Title)
{
    public override string ToString() => Title;
}

public sealed record ReminderChoice(int Minutes, string Label)
{
    public override string ToString() => Label;
}

public sealed partial class WeekdayToggle : ObservableObject
{
    [ObservableProperty] private bool _isChecked;

    public WeekdayToggle(DayOfWeek day, string label, bool isChecked)
    {
        Day = day;
        Label = label;
        _isChecked = isChecked;
    }

    public DayOfWeek Day { get; }
    public string Label { get; }
}

/// <summary>Create / edit an event: time, recurrence, reminders, linked openNotes note.</summary>
public sealed partial class EventEditorViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly INoteLauncher _noteLauncher;
    private readonly Guid _eventId;
    private readonly CalendarEvent? _original;
    private readonly DateOnly? _occurrenceDate;
    private bool _adjusting;
    private TimeSpan _timedDuration;

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _location = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private CalendarChoice? _selectedCalendar;
    [ObservableProperty] private bool _isAllDay;
    [ObservableProperty] private DateTime? _startDate;
    [ObservableProperty] private string _startTimeText = "09:00";
    [ObservableProperty] private DateTime? _endDate;
    [ObservableProperty] private string _endTimeText = "10:00";
    [ObservableProperty] private FrequencyChoice _frequency;
    [ObservableProperty] private string _intervalText = "1";
    [ObservableProperty] private RecurrenceEnd _endKind = RecurrenceEnd.Never;
    [ObservableProperty] private DateTime? _untilDate;
    [ObservableProperty] private string _countText = "10";
    [ObservableProperty] private NoteChoice? _selectedNote;
    [ObservableProperty] private ReminderChoice? _reminderToAdd;
    [ObservableProperty] private string _errorText = string.Empty;

    public EventEditorViewModel(
        IReadOnlyList<CalendarDocument> calendars,
        Guid calendarId,
        CalendarEvent ev,
        bool isNew,
        DateOnly? occurrenceDate,
        IReadOnlyList<NoteReference> notes,
        IDialogService dialogs,
        INoteLauncher noteLauncher)
    {
        _dialogs = dialogs;
        _noteLauncher = noteLauncher;
        _eventId = ev.Id;
        _original = isNew ? null : ev;
        _occurrenceDate = occurrenceDate;
        IsNew = isNew;

        Calendars = calendars.Select(c => new CalendarChoice(c.Id, c.Name, c.Color)).ToList();
        _selectedCalendar = Calendars.FirstOrDefault(c => c.Id == calendarId) ?? Calendars.FirstOrDefault();

        Frequencies = new[]
        {
            new FrequencyChoice(RecurrenceFrequency.None, "Nem ismétlődik"),
            new FrequencyChoice(RecurrenceFrequency.Daily, "Naponta"),
            new FrequencyChoice(RecurrenceFrequency.Weekly, "Hetente"),
            new FrequencyChoice(RecurrenceFrequency.Monthly, "Havonta"),
            new FrequencyChoice(RecurrenceFrequency.Yearly, "Évente"),
        };

        ReminderPresets = ReminderPlanner.Presets.Select(m => new ReminderChoice(m, ReminderPlanner.LabelOf(m))).ToList();

        NoteChoices = new[] { new NoteChoice(null, "Nincs kapcsolt jegyzet") }
            .Concat(notes.Select(n => new NoteChoice(n.Id, n.DisplayTitle)))
            .ToList();
        IsNoteSyncAvailable = notes.Count > 0 || ev.Note is not null;

        // ── load the event ──
        _title = ev.Title;
        _location = ev.Location ?? string.Empty;
        _description = ev.Description ?? string.Empty;
        _isAllDay = ev.IsAllDay;
        _startDate = ev.Start.Date;
        _endDate = ev.End.Date;
        _startTimeText = ev.Start.ToString("HH:mm");
        _endTimeText = ev.End.ToString("HH:mm");
        _timedDuration = ev.IsAllDay ? TimeSpan.FromHours(1) : (ev.End - ev.Start);

        var rule = ev.Recurrence;
        _frequency = Frequencies.First(f => f.Value == (rule?.Frequency ?? RecurrenceFrequency.None));
        _intervalText = (rule?.Interval ?? 1).ToString();
        _endKind = rule?.EndKind ?? RecurrenceEnd.Never;
        _untilDate = rule?.Until?.ToDateTime(TimeOnly.MinValue);
        _countText = (rule?.Count ?? 10).ToString();

        var selectedDays = rule?.DaysOfWeek is { Count: > 0 } days ? days : new List<DayOfWeek> { ev.Start.DayOfWeek };
        Weekdays = new ObservableCollection<WeekdayToggle>(WeekMath.OrderedDays.Select((d, i) =>
            new WeekdayToggle(d, Hu.ShortDayNames[i], selectedDays.Contains(d))));
        foreach (var day in Weekdays) day.PropertyChanged += (_, _) => OnPropertyChanged(nameof(RecurrenceSummary));

        Reminders = new ObservableCollection<ReminderChoice>(ev.ReminderMinutes.Distinct().OrderBy(m => m)
            .Select(m => new ReminderChoice(m, ReminderPlanner.LabelOf(m))));

        if (ev.Note is { } linked)
        {
            var match = NoteChoices.FirstOrDefault(n => n.Id == linked.NoteId);
            if (match is null)
            {
                match = new NoteChoice(linked.NoteId, linked.Title + " (nem elérhető)");
                NoteChoices = NoteChoices.Append(match).ToList();
            }
            _selectedNote = match;
        }
        else
        {
            _selectedNote = NoteChoices[0];
        }
    }

    public event EventHandler? CloseRequested;

    public bool IsNew { get; }
    public string WindowTitle => IsNew ? "Új esemény" : "Esemény szerkesztése";
    public EditorOutcome Outcome { get; private set; } = EditorOutcome.Cancelled;

    /// <summary>The edited event (valid after <see cref="EditorOutcome.Saved"/>).</summary>
    public CalendarEvent? Result { get; private set; }
    public Guid ResultCalendarId => SelectedCalendar?.Id ?? Guid.Empty;
    public DateOnly? OccurrenceDate => _occurrenceDate;

    public IReadOnlyList<CalendarChoice> Calendars { get; }
    public IReadOnlyList<FrequencyChoice> Frequencies { get; }
    public IReadOnlyList<ReminderChoice> ReminderPresets { get; }
    public IReadOnlyList<NoteChoice> NoteChoices { get; private set; }
    public ObservableCollection<WeekdayToggle> Weekdays { get; }
    public ObservableCollection<ReminderChoice> Reminders { get; }

    public bool IsNoteSyncAvailable { get; }
    public bool IsRecurring => Frequency.Value != RecurrenceFrequency.None;
    public bool IsWeekly => Frequency.Value == RecurrenceFrequency.Weekly;
    public bool IsExistingRecurring => _original?.IsRecurring == true;
    public bool HasLinkedNote => SelectedNote?.Id is not null;
    public bool HasReminders => Reminders.Count > 0;

    public string IntervalUnit => Frequency.Value switch
    {
        RecurrenceFrequency.Daily => "naponta",
        RecurrenceFrequency.Weekly => "hetente",
        RecurrenceFrequency.Monthly => "havonta",
        RecurrenceFrequency.Yearly => "évente",
        _ => string.Empty,
    };

    public string SeriesInfoText => _occurrenceDate is { } date
        ? $"Ismétlődő esemény – a megnyitott alkalom: {date.ToString("yyyy. MMM d.", Hu.Culture)}. A módosítás az egész sorozatra vonatkozik."
        : "Ismétlődő esemény – a módosítás az egész sorozatra vonatkozik.";

    public string RecurrenceSummary
    {
        get
        {
            if (!IsRecurring) return string.Empty;
            var interval = ParseInt(IntervalText, 1);
            var every = interval == 1 ? "Minden" : $"Minden {interval}.";
            var text = Frequency.Value switch
            {
                RecurrenceFrequency.Daily => $"{every} nap",
                RecurrenceFrequency.Weekly => $"{every} héten: " + string.Join(", ", Weekdays.Where(d => d.IsChecked).Select(d => d.Label)),
                RecurrenceFrequency.Monthly => $"{every} hónap {StartDate?.Day}. napján",
                RecurrenceFrequency.Yearly => $"{every} évben, {StartDate?.ToString("MMMM d.", Hu.Culture)}",
                _ => string.Empty,
            };
            return EndKind switch
            {
                RecurrenceEnd.OnDate when UntilDate is { } until => $"{text} · {until.ToString("yyyy. MMM d.", Hu.Culture)}-ig",
                RecurrenceEnd.AfterCount => $"{text} · {ParseInt(CountText, 1)} alkalommal",
                _ => text,
            };
        }
    }

    // ───────────────────────── commands ─────────────────────────

    [RelayCommand]
    private void RemoveReminder(ReminderChoice? reminder)
    {
        if (reminder is null) return;
        Reminders.Remove(reminder);
        OnPropertyChanged(nameof(HasReminders));
    }

    [RelayCommand]
    private void OpenNote()
    {
        if (SelectedNote?.Id is not { } id) return;
        if (!_noteLauncher.TryOpen(id))
            _dialogs.ShowError("openNotes nem található", "Telepítsd vagy indítsd el egyszer az openNotes alkalmazást, hogy a naptár megtalálja.");
    }

    [RelayCommand]
    private void ClearNote() => SelectedNote = NoteChoices[0];

    [RelayCommand]
    private void Save()
    {
        if (!TryBuild(out var ev)) return;
        Result = ev;
        Outcome = EditorOutcome.Saved;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Delete()
    {
        if (_original is null) return;

        if (_original.IsRecurring && _occurrenceDate is not null)
        {
            var choice = _dialogs.Ask("Ismétlődő esemény törlése",
                "Csak ezt az alkalmat törlöd, vagy a teljes sorozatot?", "Csak ezt az alkalmat", "Az egész sorozatot");
            if (choice == DialogChoice.Cancel) return;
            Outcome = choice == DialogChoice.Primary ? EditorOutcome.OccurrenceDeleted : EditorOutcome.Deleted;
        }
        else
        {
            if (!_dialogs.Confirm("Esemény törlése", $"Biztosan törlöd: „{_original.DisplayTitle}”?", "Törlés", destructive: true)) return;
            Outcome = EditorOutcome.Deleted;
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        Outcome = EditorOutcome.Cancelled;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    // ───────────────────────── change handling ─────────────────────────

    partial void OnReminderToAddChanged(ReminderChoice? value)
    {
        if (value is null) return;
        if (Reminders.All(r => r.Minutes != value.Minutes))
        {
            var index = Reminders.TakeWhile(r => r.Minutes < value.Minutes).Count();
            Reminders.Insert(index, value);
            OnPropertyChanged(nameof(HasReminders));
        }
        // Reset so the same preset can be picked again later.
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() => ReminderToAdd = null);
    }

    partial void OnFrequencyChanged(FrequencyChoice value)
    {
        OnPropertyChanged(nameof(IsRecurring));
        OnPropertyChanged(nameof(IsWeekly));
        OnPropertyChanged(nameof(IntervalUnit));
        OnPropertyChanged(nameof(RecurrenceSummary));
    }

    partial void OnIntervalTextChanged(string value) => OnPropertyChanged(nameof(RecurrenceSummary));
    partial void OnEndKindChanged(RecurrenceEnd value) => OnPropertyChanged(nameof(RecurrenceSummary));
    partial void OnCountTextChanged(string value) => OnPropertyChanged(nameof(RecurrenceSummary));

    partial void OnUntilDateChanged(DateTime? value)
    {
        if (value is not null && EndKind != RecurrenceEnd.OnDate) EndKind = RecurrenceEnd.OnDate;
        OnPropertyChanged(nameof(RecurrenceSummary));
    }

    partial void OnSelectedNoteChanged(NoteChoice? value) => OnPropertyChanged(nameof(HasLinkedNote));

    /// <summary>Moving the start keeps the duration (like other calendars).</summary>
    partial void OnStartDateChanged(DateTime? oldValue, DateTime? newValue)
    {
        OnPropertyChanged(nameof(RecurrenceSummary));
        if (_adjusting || oldValue is null || newValue is null || EndDate is null) return;

        _adjusting = true;
        EndDate = EndDate.Value + (newValue.Value - oldValue.Value);
        _adjusting = false;
    }

    partial void OnStartTimeTextChanged(string value)
    {
        if (_adjusting || !TryCombine(StartDate, value, out var start)) return;
        _adjusting = true;
        var end = start + _timedDuration;
        EndDate = end.Date;
        EndTimeText = end.ToString("HH:mm");
        _adjusting = false;
    }

    partial void OnEndTimeTextChanged(string value) => RememberDuration();

    partial void OnEndDateChanged(DateTime? value) => RememberDuration();

    private void RememberDuration()
    {
        if (_adjusting) return;
        if (TryCombine(StartDate, StartTimeText, out var start) && TryCombine(EndDate, EndTimeText, out var end) && end > start)
            _timedDuration = end - start;
    }

    // ───────────────────────── build ─────────────────────────

    private bool TryBuild(out CalendarEvent ev)
    {
        ev = null!;
        ErrorText = string.Empty;

        if (SelectedCalendar is null) return Fail("Válassz naptárt.");
        if (StartDate is null) return Fail("Add meg a kezdés dátumát.");

        DateTime start, end;
        if (IsAllDay)
        {
            start = StartDate.Value.Date;
            end = (EndDate ?? StartDate.Value).Date;
            if (end < start) return Fail("A befejezés nem lehet a kezdés előtt.");
        }
        else
        {
            if (!TryCombine(StartDate, StartTimeText, out start)) return Fail("Érvénytelen kezdési időpont (pl. 09:30).");
            if (!TryCombine(EndDate ?? StartDate, EndTimeText, out end)) return Fail("Érvénytelen befejezési időpont (pl. 10:30).");
            if (end <= start) return Fail("A befejezésnek a kezdés utánra kell esnie.");
        }

        RecurrenceRule? rule = null;
        if (IsRecurring)
        {
            rule = new RecurrenceRule
            {
                Frequency = Frequency.Value,
                Interval = Math.Clamp(ParseInt(IntervalText, 1), 1, 999),
                EndKind = EndKind,
                DaysOfWeek = IsWeekly ? Weekdays.Where(d => d.IsChecked).Select(d => d.Day).ToList() : new List<DayOfWeek>(),
            };
            if (IsWeekly && rule.DaysOfWeek.Count == 0) rule.DaysOfWeek.Add(start.DayOfWeek);

            if (EndKind == RecurrenceEnd.OnDate)
            {
                if (UntilDate is null) return Fail("Add meg, meddig ismétlődjön.");
                if (UntilDate.Value.Date < start.Date) return Fail("Az ismétlődés vége nem lehet a kezdés előtt.");
                rule.Until = DateOnly.FromDateTime(UntilDate.Value);
            }
            else if (EndKind == RecurrenceEnd.AfterCount)
            {
                rule.Count = Math.Clamp(ParseInt(CountText, 1), 1, 9999);
            }
        }

        ev = _original?.Clone() ?? new CalendarEvent { Id = _eventId };
        ev.Title = Title.Trim();
        ev.Location = string.IsNullOrWhiteSpace(Location) ? null : Location.Trim();
        ev.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        ev.IsAllDay = IsAllDay;
        ev.Start = start;
        ev.End = end;
        ev.Recurrence = rule;
        if (rule is null) ev.ExceptionDates.Clear();
        ev.ReminderMinutes = Reminders.Select(r => r.Minutes).ToList();
        ev.Note = SelectedNote?.Id is { } noteId ? new LinkedNote(noteId, SelectedNote.Title.Replace(" (nem elérhető)", string.Empty)) : null;
        return true;
    }

    private bool Fail(string message)
    {
        ErrorText = message;
        return false;
    }

    private static bool TryCombine(DateTime? date, string? timeText, out DateTime result)
    {
        result = default;
        if (date is null || !TimeInput.TryParse(timeText, out var time) || time is null) return false;
        result = date.Value.Date + time.Value.ToTimeSpan();
        return true;
    }

    private static int ParseInt(string? text, int fallback) => int.TryParse(text, out var value) && value > 0 ? value : fallback;
}
