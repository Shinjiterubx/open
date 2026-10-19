using OpenNotes.Core.Abstractions;
using OpenNotes.UI.ViewModels;

namespace OpenNotes.UI.Services;

/// <summary>Binds the configured global hotkeys to app actions.</summary>
public sealed class AppHotkeys
{
    private readonly IGlobalHotkeyService _hotkeys;
    private readonly ISettingsStore _settings;
    private readonly MainViewModel _main;
    private readonly IWindowService _windows;

    public AppHotkeys(IGlobalHotkeyService hotkeys, ISettingsStore settings, MainViewModel main, IWindowService windows)
    {
        _hotkeys = hotkeys;
        _settings = settings;
        _main = main;
        _windows = windows;
    }

    /// <summary>(Re)registers all hotkeys. Returns a description of every hotkey that could not be registered.</summary>
    public IReadOnlyList<string> Apply()
    {
        _hotkeys.UnregisterAll();

        var settings = _settings.Current;
        var failures = new List<string>();

        if (!_hotkeys.TryRegister(settings.OpenLastNoteHotkey, OpenLastNote))
            failures.Add($"Utolsó jegyzet ({settings.OpenLastNoteHotkey})");

        if (!_hotkeys.TryRegister(settings.NewNoteHotkey, NewNote))
            failures.Add($"Új jegyzet ({settings.NewNoteHotkey})");

        return failures;
    }

    private void OpenLastNote()
    {
        // Pressing the hotkey again while the quick window is focused hides it (toggle).
        if (_windows.HideQuickWindowIfActive()) return;

        _main.OpenLastNote();
        _windows.ShowQuickWindow();
    }

    private void NewNote()
    {
        _main.NewNote();
        _windows.ShowQuickWindow(focusTitle: true);
    }
}
