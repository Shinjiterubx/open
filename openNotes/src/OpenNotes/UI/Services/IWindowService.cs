namespace OpenNotes.UI.Services;

public interface IWindowService
{
    void ShowMain();

    void ShowMini();

    /// <summary>
    /// Opens the window the user last used: the mini window with the last note when they left the app
    /// in mini mode, otherwise the main window. Used for app launch, tray click and second launches.
    /// </summary>
    void ShowLastUsed();

    /// <summary>Opens the window chosen in settings for global hotkeys.</summary>
    void ShowQuickWindow(bool focusTitle = false);

    /// <summary>Hides the quick window if it is the active window. Returns <c>true</c> if it was hidden.</summary>
    bool HideQuickWindowIfActive();

    /// <summary>Switches between the main window and the mini window.</summary>
    void ToggleMiniMode();

    void ShowSettings();

    /// <summary>Shows the "remaining tasks" summary window.</summary>
    void ShowReminderSummary();

    /// <summary>Opens the full-screen image viewer (zoom, pan, copy, save).</summary>
    void ShowImage(byte[] data, string fileName);

    /// <summary>Moves keyboard focus to the title of the current note in the visible window.</summary>
    void FocusTitle();
}
