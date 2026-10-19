using OpenCalendar.Core.Abstractions;
using OpenCalendar.Core.Agenda;
using OpenCalendar.Core.Models;

namespace OpenCalendar.Services;

/// <summary>
/// Application service: owns the calendars in memory, persists every change and merges in the
/// openNotes deadlines. View models talk to this class only (never to storage directly).
/// Must be used from the UI thread; <see cref="Changed"/> is always raised on the UI thread.
/// </summary>
public sealed class CalendarService
{
    private readonly ICalendarRepository _repository;
    private readonly ICalendarSerializer _serializer;
    private readonly INoteSource _notes;
    private readonly IKanbanSource _kanban;
    private readonly ISettingsStore _settings;
    private readonly List<CalendarDocument> _calendars = new();
    private readonly System.Windows.Threading.Dispatcher _dispatcher;

    public CalendarService(ICalendarRepository repository, ICalendarSerializer serializer, INoteSource notes, IKanbanSource kanban, ISettingsStore settings)
    {
        _repository = repository;
        _serializer = serializer;
        _notes = notes;
        _kanban = kanban;
        _settings = settings;
        _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        _notes.NotesChanged += (_, _) => _dispatcher.InvokeAsync(RaiseChanged);
        _kanban.CardsChanged += (_, _) => _dispatcher.InvokeAsync(RaiseChanged);
    }

    /// <summary>Raised after calendars, events or synced notes changed.</summary>
    public event EventHandler? Changed;

    public IReadOnlyList<CalendarDocument> Calendars => _calendars;

    public IReadOnlyList<NoteReference> Notes => _settings.Current.OpenNotesSyncEnabled ? _notes.Notes : Array.Empty<NoteReference>();

    public string? NotesFolder => _notes.NotesFolder;

    public IReadOnlyList<KanbanCardReference> KanbanCards =>
        _settings.Current.KanbanSyncEnabled ? _kanban.Cards : Array.Empty<KanbanCardReference>();

    public bool IsKanbanAvailable => _kanban.IsAvailable;

    public IReadOnlyList<string> LoadErrors => _repository.LoadErrors;

    public void Load()
    {
        _calendars.Clear();
        _calendars.AddRange(_repository.LoadAll());

        if (_calendars.Count == 0)
            AddCalendar("Személyes", CalendarDocument.Palette[0]);

        RestartNoteSync();
        RaiseChanged();
    }

    public void RestartNoteSync()
    {
        if (_settings.Current.OpenNotesSyncEnabled) _notes.Start(_settings.Current.OpenNotesFolder);
        else _notes.Stop();

        if (_settings.Current.KanbanSyncEnabled) _kanban.Start();
        else _kanban.Stop();
        RaiseChanged();
    }

    /// <summary>Entries of visible calendars (and openNotes deadlines if shown) overlapping [from, to).</summary>
    public IReadOnlyList<CalendarEntry> Query(DateTime from, DateTime to)
    {
        var settings = _settings.Current;
        var notes = settings.OpenNotesCalendarVisible ? Notes : Array.Empty<NoteReference>();
        var cards = settings.KanbanCalendarVisible ? KanbanCards : Array.Empty<KanbanCardReference>();
        return AgendaBuilder.Build(_calendars.Where(c => c.IsVisible), notes, settings.ShowDoneNotes, from, to, cards);
    }

    public CalendarDocument? FindCalendar(Guid id) => _calendars.FirstOrDefault(c => c.Id == id);

    public (CalendarDocument Calendar, CalendarEvent Event)? FindEvent(Guid eventId)
    {
        foreach (var calendar in _calendars)
        {
            var ev = calendar.Events.FirstOrDefault(e => e.Id == eventId);
            if (ev is not null) return (calendar, ev);
        }
        return null;
    }

    // ───────────────────────── calendars ─────────────────────────

    public CalendarDocument AddCalendar(string name, string color)
    {
        var calendar = new CalendarDocument { Name = name.Trim().Length == 0 ? "Naptár" : name.Trim(), Color = color };
        _calendars.Add(calendar);
        Persist(calendar);
        return calendar;
    }

    public void UpdateCalendar(CalendarDocument calendar, string name, string color)
    {
        calendar.Name = name.Trim().Length == 0 ? calendar.Name : name.Trim();
        calendar.Color = color;
        Persist(calendar);
    }

    public void SetVisible(CalendarDocument calendar, bool visible)
    {
        if (calendar.IsVisible == visible) return;
        calendar.IsVisible = visible;
        Persist(calendar);
    }

    public void DeleteCalendar(CalendarDocument calendar)
    {
        _repository.Delete(calendar.Id);
        _calendars.Remove(calendar);
        RaiseChanged();
    }

    // ───────────────────────── events ─────────────────────────

    /// <summary>Adds or replaces an event (moving it between calendars if needed).</summary>
    public void SaveEvent(Guid calendarId, CalendarEvent ev)
    {
        var target = FindCalendar(calendarId) ?? throw new InvalidOperationException("A naptár nem található.");
        ev.UpdatedAt = DateTimeOffset.Now;

        if (FindEvent(ev.Id) is { } existing)
        {
            existing.Calendar.Events.Remove(existing.Event);
            if (existing.Calendar != target)
            {
                existing.Calendar.UpdatedAt = DateTimeOffset.Now;
                _repository.Save(existing.Calendar);
            }
        }

        target.Events.Add(ev);
        Persist(target);
    }

    public void DeleteEvent(Guid eventId)
    {
        if (FindEvent(eventId) is not { } found) return;
        found.Calendar.Events.Remove(found.Event);
        Persist(found.Calendar);
    }

    /// <summary>Removes one occurrence of a recurring event ("only this one").</summary>
    public void DeleteOccurrence(Guid eventId, DateOnly date)
    {
        if (FindEvent(eventId) is not { } found) return;
        if (!found.Event.ExceptionDates.Contains(date)) found.Event.ExceptionDates.Add(date);
        found.Event.UpdatedAt = DateTimeOffset.Now;
        Persist(found.Calendar);
    }

    // ───────────────────────── import / export ─────────────────────────

    public void Export(IReadOnlyCollection<CalendarDocument> calendars, string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        _serializer.Write(calendars, stream);
    }

    public IReadOnlyList<CalendarDocument> ReadFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return _serializer.Read(stream);
    }

    /// <param name="replaceExisting">If a calendar with the same id exists: replace it (<c>true</c>) or add a copy (<c>false</c>).</param>
    public void Import(CalendarDocument calendar, bool replaceExisting)
    {
        var existing = FindCalendar(calendar.Id);
        if (existing is not null && replaceExisting)
        {
            _calendars[_calendars.IndexOf(existing)] = calendar;
        }
        else
        {
            if (existing is not null)
            {
                calendar.Id = Guid.NewGuid();
                calendar.Name += " (importált)";
                foreach (var ev in calendar.Events) ev.Id = Guid.NewGuid();
            }
            _calendars.Add(calendar);
        }

        calendar.IsVisible = true;
        Persist(calendar);
    }

    private void Persist(CalendarDocument calendar)
    {
        calendar.UpdatedAt = DateTimeOffset.Now;
        _repository.Save(calendar);
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
