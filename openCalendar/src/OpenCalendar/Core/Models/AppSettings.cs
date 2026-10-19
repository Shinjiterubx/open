namespace OpenCalendar.Core.Models;

public enum CalendarViewMode
{
    Day,
    Week,
    Month,
    Agenda,
}

public sealed class WindowBounds
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsMaximized { get; set; }
}

/// <summary>User settings persisted as JSON. Add new properties with sensible defaults.</summary>
public sealed class AppSettings
{
    public bool StartWithWindows { get; set; } = true;
    public bool StartMinimized { get; set; }
    public bool CloseToTray { get; set; } = true;

    public CalendarViewMode ViewMode { get; set; } = CalendarViewMode.Week;
    public TimeOnly WorkdayStart { get; set; } = new(8, 0);
    public TimeOnly WorkdayEnd { get; set; } = new(17, 0);
    public int DefaultDurationMinutes { get; set; } = 60;

    /// <summary>Reminder offsets applied to new events (minutes before start).</summary>
    public List<int> DefaultReminderMinutes { get; set; } = new() { 15 };

    public bool PlayReminderSound { get; set; } = true;

    // openNotes integration
    public bool OpenNotesSyncEnabled { get; set; } = true;

    /// <summary>Custom openNotes notes folder; <c>null</c> = detect automatically.</summary>
    public string? OpenNotesFolder { get; set; }

    public bool ShowDoneNotes { get; set; }
    public bool OpenNotesCalendarVisible { get; set; } = true;

    // openKanban integration
    public bool KanbanSyncEnabled { get; set; } = true;
    public bool KanbanCalendarVisible { get; set; } = true;

    /// <summary>Remind about openNotes deadlines that have a time (minutes before).</summary>
    public int OpenNotesReminderMinutes { get; set; } = 15;

    public WindowBounds? MainWindowBounds { get; set; }
    public double SidebarWidth { get; set; } = 280;
}
