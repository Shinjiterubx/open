namespace OpenLauncher.Core.Models;

public sealed class WindowBounds
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool IsMaximized { get; set; }
}

/// <summary>openLauncher keeps very little state: the window position only.</summary>
public sealed class AppSettings
{
    public WindowBounds? MainWindowBounds { get; set; }
}
