using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Models;
using OpenNotes.Core.Reminders;
using OpenNotes.Infrastructure;
using OpenNotes.UI.Services;

namespace OpenNotes.UI.ViewModels;

/// <summary>Editable copy of the settings; nothing is applied until <see cref="Save"/>.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private const double MinFontSize = 12;
    private const double MaxFontSize = 24;

    private readonly ISettingsStore _store;
    private readonly IAutoStartService _autoStart;
    private readonly AppHotkeys _hotkeys;
    private readonly IAppPaths _paths;

    [ObservableProperty] private HotkeyGesture _openLastNoteHotkey;
    [ObservableProperty] private HotkeyGesture _newNoteHotkey;
    [ObservableProperty] private HotkeyTarget _hotkeyTarget;
    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _closeToTray;
    [ObservableProperty] private double _editorFontSize;
    [ObservableProperty] private string _errorText = string.Empty;
    [ObservableProperty] private bool _dailySummaryEnabled;
    [ObservableProperty] private string _dailySummaryTimeText = "09:00";

    public SettingsViewModel(ISettingsStore store, IAutoStartService autoStart, AppHotkeys hotkeys, IAppPaths paths)
    {
        _store = store;
        _autoStart = autoStart;
        _hotkeys = hotkeys;
        _paths = paths;

        var current = store.Current;
        _openLastNoteHotkey = current.OpenLastNoteHotkey;
        _newNoteHotkey = current.NewNoteHotkey;
        _hotkeyTarget = current.HotkeyTarget;
        _startWithWindows = autoStart.IsEnabled;
        _startMinimized = current.StartMinimized;
        _closeToTray = current.CloseToTray;
        _editorFontSize = current.EditorFontSize;
        _dailySummaryEnabled = current.DailySummaryEnabled;
        _dailySummaryTimeText = current.DailySummaryTime.ToString("HH:mm");
    }

    public string NotesDirectory => _paths.NotesDirectory;
    public string VersionText => $"{AppInfo.Name} {AppInfo.Version}  ·  MIT licenc  ·  Nyílt forráskódú szoftver";
    public string FontSizeText => $"{EditorFontSize:0} px";

    /// <summary>Applies and persists the settings. Returns <c>false</c> (with <see cref="ErrorText"/>) if something needs attention.</summary>
    public bool Save()
    {
        ErrorText = string.Empty;

        if (!TimeInput.TryParse(DailySummaryTimeText, out var summaryTime) || summaryTime is null)
        {
            ErrorText = "Az összegző időpontja érvénytelen (pl. 09:00).";
            return false;
        }

        if (OpenLastNoteHotkey == NewNoteHotkey)
        {
            ErrorText = "A két gyorsbillentyű nem lehet azonos.";
            return false;
        }

        var settings = _store.Current;
        var previousOpen = settings.OpenLastNoteHotkey;
        var previousNew = settings.NewNoteHotkey;

        settings.OpenLastNoteHotkey = OpenLastNoteHotkey;
        settings.NewNoteHotkey = NewNoteHotkey;

        var failures = _hotkeys.Apply();
        if (failures.Count > 0)
        {
            settings.OpenLastNoteHotkey = previousOpen;
            settings.NewNoteHotkey = previousNew;
            _hotkeys.Apply();
            ErrorText = "Ezt a kombinációt már egy másik program használja: " + string.Join(", ", failures);
            return false;
        }

        try
        {
            _autoStart.SetEnabled(StartWithWindows);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            ErrorText = "Az automatikus indítás nem állítható be: " + ex.Message;
            return false;
        }

        settings.HotkeyTarget = HotkeyTarget;
        settings.StartWithWindows = StartWithWindows;
        settings.StartMinimized = StartMinimized;
        settings.CloseToTray = CloseToTray;
        settings.EditorFontSize = EditorFontSize;
        settings.DailySummaryEnabled = DailySummaryEnabled;
        if (settings.DailySummaryTime != summaryTime.Value)
        {
            settings.DailySummaryTime = summaryTime.Value;
            // A later time today should still trigger today's summary.
            if (TimeOnly.FromDateTime(DateTime.Now) < summaryTime.Value) settings.LastDailySummaryDate = null;
        }

        _store.Save();
        return true;
    }

    [RelayCommand]
    private void ResetHotkeys()
    {
        OpenLastNoteHotkey = HotkeyGesture.DefaultOpenLastNote;
        NewNoteHotkey = HotkeyGesture.DefaultNewNote;
    }

    [RelayCommand]
    private void IncreaseFontSize() => EditorFontSize = Math.Min(MaxFontSize, EditorFontSize + 1);

    [RelayCommand]
    private void DecreaseFontSize() => EditorFontSize = Math.Max(MinFontSize, EditorFontSize - 1);

    [RelayCommand]
    private void OpenNotesFolder() =>
        Process.Start(new ProcessStartInfo(_paths.NotesDirectory) { UseShellExecute = true });

    partial void OnEditorFontSizeChanged(double value) => OnPropertyChanged(nameof(FontSizeText));
}
