namespace OpenKanban.Core.Models;

public sealed class WindowBounds
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsMaximized { get; set; }
}

/// <summary>User settings persisted as JSON.</summary>
public sealed class AppSettings
{
    public Guid? LastWorkspaceId { get; set; }
    public Guid? LastBoardId { get; set; }

    /// <summary>Width of a column on the board.</summary>
    public double ColumnWidth { get; set; } = 290;

    /// <summary>Show the "done" checkmark and fade done cards.</summary>
    public bool FadeDoneCards { get; set; } = true;

    public bool OpenNotesIntegration { get; set; } = true;
    public bool OpenCalendarIntegration { get; set; } = true;

    public WindowBounds? MainWindowBounds { get; set; }
    public double SidebarWidth { get; set; } = 260;
}
