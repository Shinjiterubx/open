using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OpenCalendar.Core.Abstractions;
using OpenCalendar.Core.Reminders;
using OpenCalendar.Core.Time;
using OpenCalendar.Infrastructure;
using OpenCalendar.Infrastructure.Integration;
using OpenCalendar.Services;

namespace OpenCalendar.UI.ViewModels;

/// <summary>Editable copy of the settings; applied on <see cref="Save"/>.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly IAutoStartService _autoStart;
    private readonly IAppPaths _paths;
    private readonly CalendarService _calendars;

    [ObservableProperty] private bool _startWithWindows;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _closeToTray;
    [ObservableProperty] private string _workdayStartText;
    [ObservableProperty] private string _workdayEndText;
    [ObservableProperty] private string _defaultDurationText;
    [ObservableProperty] private ReminderChoice? _defaultReminder;
    [ObservableProperty] private bool _playReminderSound;
    [ObservableProperty] private bool _openNotesSyncEnabled;
    [ObservableProperty] private string _openNotesFolder;
    [ObservableProperty] private bool _showDoneNotes;
    [ObservableProperty] private ReminderChoice? _noteReminder;
    [ObservableProperty] private string _errorText = string.Empty;
    [ObservableProperty] private bool _kanbanSyncEnabled;

    public SettingsViewModel(ISettingsStore store, IAutoStartService autoStart, IAppPaths paths, CalendarService calendars)
    {
        _store = store;
        _autoStart = autoStart;
        _paths = paths;
        _calendars = calendars;

        var s = store.Current;
        _startWithWindows = autoStart.IsEnabled;
        _startMinimized = s.StartMinimized;
        _closeToTray = s.CloseToTray;
        _workdayStartText = s.WorkdayStart.ToString("HH:mm");
        _workdayEndText = s.WorkdayEnd.ToString("HH:mm");
        _defaultDurationText = s.DefaultDurationMinutes.ToString();
        _playReminderSound = s.PlayReminderSound;
        _openNotesSyncEnabled = s.OpenNotesSyncEnabled;
        _openNotesFolder = s.OpenNotesFolder ?? string.Empty;
        _showDoneNotes = s.ShowDoneNotes;
        _kanbanSyncEnabled = s.KanbanSyncEnabled;

        ReminderOptions = new[] { new ReminderChoice(-1, "Nincs") }
            .Concat(ReminderPlanner.Presets.Select(m => new ReminderChoice(m, ReminderPlanner.LabelOf(m))))
            .ToList();
        var defaultMinutes = s.DefaultReminderMinutes.Count > 0 ? s.DefaultReminderMinutes[0] : -1;
        _defaultReminder = ReminderOptions.FirstOrDefault(r => r.Minutes == defaultMinutes) ?? ReminderOptions[0];
        _noteReminder = ReminderOptions.FirstOrDefault(r => r.Minutes == s.OpenNotesReminderMinutes) ?? ReminderOptions[0];
    }

    public IReadOnlyList<ReminderChoice> ReminderOptions { get; }
    public string CalendarsDirectory => _paths.CalendarsDirectory;
    public string VersionText => $"{AppInfo.Name} {AppInfo.Version}  ·  MIT licenc  ·  Nyílt forráskódú szoftver";

    public string DetectedNotesFolderText =>
        OpenNotesLocator.FindNotesFolder() is { } folder ? $"Automatikusan felismert mappa: {folder}" : "Az openNotes mappája nem található automatikusan.";

    public bool Save()
    {
        ErrorText = string.Empty;

        if (!TimeInput.TryParse(WorkdayStartText, out var workStart) || workStart is null ||
            !TimeInput.TryParse(WorkdayEndText, out var workEnd) || workEnd is null || workEnd <= workStart)
        {
            ErrorText = "A munkaidő kezdete és vége érvénytelen (pl. 08:00 – 17:00).";
            return false;
        }

        if (!int.TryParse(DefaultDurationText, out var duration) || duration < 5 || duration > 24 * 60)
        {
            ErrorText = "Az alapértelmezett hossz 5 és 1440 perc között lehet.";
            return false;
        }

        var folder = OpenNotesFolder.Trim();
        if (folder.Length > 0 && !Directory.Exists(folder))
        {
            ErrorText = "A megadott openNotes mappa nem létezik.";
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

        var s = _store.Current;
        var syncChanged = s.OpenNotesSyncEnabled != OpenNotesSyncEnabled || (s.OpenNotesFolder ?? string.Empty) != folder
                          || s.KanbanSyncEnabled != KanbanSyncEnabled;

        s.StartWithWindows = StartWithWindows;
        s.StartMinimized = StartMinimized;
        s.CloseToTray = CloseToTray;
        s.WorkdayStart = workStart.Value;
        s.WorkdayEnd = workEnd.Value;
        s.DefaultDurationMinutes = duration;
        s.DefaultReminderMinutes = DefaultReminder is { Minutes: >= 0 } reminder ? new List<int> { reminder.Minutes } : new List<int>();
        s.PlayReminderSound = PlayReminderSound;
        s.OpenNotesSyncEnabled = OpenNotesSyncEnabled;
        s.OpenNotesFolder = folder.Length == 0 ? null : folder;
        s.ShowDoneNotes = ShowDoneNotes;
        s.KanbanSyncEnabled = KanbanSyncEnabled;
        s.OpenNotesReminderMinutes = NoteReminder?.Minutes ?? -1;
        _store.Save();

        if (syncChanged) _calendars.RestartNoteSync();
        return true;
    }

    [RelayCommand]
    private void BrowseNotesFolder()
    {
        var dialog = new OpenFolderDialog { Title = "openNotes jegyzetek mappája" };
        if (dialog.ShowDialog() == true) OpenNotesFolder = dialog.FolderName;
    }

    [RelayCommand]
    private void OpenCalendarsFolder() =>
        Process.Start(new ProcessStartInfo(_paths.CalendarsDirectory) { UseShellExecute = true });
}
