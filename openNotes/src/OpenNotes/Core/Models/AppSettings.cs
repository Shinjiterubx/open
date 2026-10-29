namespace OpenNotes.Core.Models;

public enum ViewMode
{
    Editor,
    Split,
    Preview,
}

/// <summary>The app's two windows.</summary>
public enum AppWindow
{
    Main,
    Mini,
}

/// <summary>Which window the global hotkeys bring up.</summary>
public enum HotkeyTarget
{
    MiniWindow,
    MainWindow,
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
    public HotkeyGesture OpenLastNoteHotkey { get; set; } = HotkeyGesture.DefaultOpenLastNote;
    public HotkeyGesture NewNoteHotkey { get; set; } = HotkeyGesture.DefaultNewNote;
    public HotkeyTarget HotkeyTarget { get; set; } = HotkeyTarget.MiniWindow;

    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool CloseToTray { get; set; } = true;

    public double EditorFontSize { get; set; } = 15;
    public ViewMode ViewMode { get; set; } = ViewMode.Split;

    public Guid? LastOpenedNoteId { get; set; }

    /// <summary>The window the user last switched to; launching the app reopens it (with the last note).</summary>
    public AppWindow LastUsedWindow { get; set; } = AppWindow.Main;
    /// <summary>Show the "remaining tasks" summary once a day (at start-up and at <see cref="DailySummaryTime"/>).</summary>
    public bool DailySummaryEnabled { get; set; } = true;
    public TimeOnly DailySummaryTime { get; set; } = new(9, 0);
    public DateOnly? LastDailySummaryDate { get; set; }
    public bool ReminderPanelExpanded { get; set; } = true;
    public bool FolderPanelExpanded { get; set; } = true;

    public WindowBounds? MainWindowBounds { get; set; }
    public WindowBounds? MiniWindowBounds { get; set; }
    public double SidebarWidth { get; set; } = 300;
}
