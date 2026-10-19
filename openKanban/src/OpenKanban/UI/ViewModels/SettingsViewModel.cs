using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenKanban.Core.Abstractions;
using OpenKanban.Infrastructure;

namespace OpenKanban.UI.ViewModels;

/// <summary>Editable copy of the settings; applied on <see cref="Save"/>.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly IAppPaths _paths;
    private readonly ICompanionLauncher _companions;

    [ObservableProperty] private double _columnWidth;
    [ObservableProperty] private bool _fadeDoneCards;
    [ObservableProperty] private bool _openNotesIntegration;
    [ObservableProperty] private bool _openCalendarIntegration;

    public SettingsViewModel(ISettingsStore store, IAppPaths paths, ICompanionLauncher companions)
    {
        _store = store;
        _paths = paths;
        _companions = companions;

        var s = store.Current;
        _columnWidth = s.ColumnWidth;
        _fadeDoneCards = s.FadeDoneCards;
        _openNotesIntegration = s.OpenNotesIntegration;
        _openCalendarIntegration = s.OpenCalendarIntegration;
    }

    public string WorkspacesDirectory => _paths.WorkspacesDirectory;
    public string VersionText => $"{AppInfo.Name} {AppInfo.Version}  ·  MIT licenc  ·  Nyílt forráskódú szoftver";
    public string ColumnWidthText => $"{ColumnWidth:0} px";

    public string NotesStatus => _companions.IsOpenNotesAvailable ? "openNotes telepítve – a kártyákhoz jegyzet kapcsolható" : "openNotes nincs telepítve";
    public string CalendarStatus => _companions.IsOpenCalendarAvailable
        ? "openCalendar telepítve – a határidős kártyák megjelennek a naptárban"
        : "openCalendar nincs telepítve";

    public void Save()
    {
        var s = _store.Current;
        s.ColumnWidth = ColumnWidth;
        s.FadeDoneCards = FadeDoneCards;
        s.OpenNotesIntegration = OpenNotesIntegration;
        s.OpenCalendarIntegration = OpenCalendarIntegration;
        _store.Save();
    }

    [RelayCommand]
    private void Narrower() => ColumnWidth = Math.Max(230, ColumnWidth - 20);

    [RelayCommand]
    private void Wider() => ColumnWidth = Math.Min(420, ColumnWidth + 20);

    [RelayCommand]
    private void OpenDataFolder() => Process.Start(new ProcessStartInfo(_paths.WorkspacesDirectory) { UseShellExecute = true });

    partial void OnColumnWidthChanged(double value) => OnPropertyChanged(nameof(ColumnWidthText));
}
