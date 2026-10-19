using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenLauncher.Core.Abstractions;
using OpenLauncher.Infrastructure;
using OpenLauncher.UI.Services;

namespace OpenLauncher.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAppPaths _paths;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;

    public MainViewModel(IAppCatalog catalog, IInstallationService installer, IAppPaths paths,
        IDialogService dialogs, IShellService shell)
    {
        _paths = paths;
        _dialogs = dialogs;
        _shell = shell;
        Apps = new ObservableCollection<AppCardViewModel>(
            catalog.Apps.Select(app => new AppCardViewModel(app, installer, dialogs, shell, UpdateSummary)));
    }

    public ObservableCollection<AppCardViewModel> Apps { get; }

    public string VersionText => $"openLauncher {AppInfo.Version}";

    [ObservableProperty]
    private string _summaryText = string.Empty;

    /// <summary>Re-reads every app's state. Cheap (registry + process list), called on window activation.</summary>
    [RelayCommand]
    public void Refresh()
    {
        foreach (var app in Apps.Where(a => !a.IsBusy)) app.Refresh();
        UpdateSummary();
    }

    [RelayCommand]
    private void OpenInstallersFolder()
    {
        if (!_shell.OpenFolder(_paths.InstallersDirectory))
            _dialogs.ShowError("Nincsenek telepítők",
                $"A telepítők mappája nem található:\n{_paths.InstallersDirectory}\n\nAz openLauncher telepítője hozza létre (build-all.ps1).");
    }

    [RelayCommand]
    private void OpenLogFolder() => _shell.OpenFolder(_paths.LogDirectory);

    private void UpdateSummary()
    {
        var installed = Apps.Count(a => a.IsInstalled || a.State == AppState.Portable);
        SummaryText = $"{installed} / {Apps.Count} alkalmazás telepítve";
    }
}
