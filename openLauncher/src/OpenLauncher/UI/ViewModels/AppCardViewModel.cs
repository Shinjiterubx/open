using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenLauncher.Core.Abstractions;
using OpenLauncher.Core.Models;
using OpenLauncher.Core.Versioning;
using OpenLauncher.UI.Services;

namespace OpenLauncher.UI.ViewModels;

public enum AppState
{
    NotInstalled,
    Installed,
    Portable,
}

/// <summary>One app tile: status + install / uninstall / launch.</summary>
public sealed partial class AppCardViewModel : ObservableObject
{
    private readonly IInstallationService _installer;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly Action _changed;

    public AppCardViewModel(AppDefinition definition, IInstallationService installer, IDialogService dialogs,
        IShellService shell, Action changed)
    {
        Definition = definition;
        _installer = installer;
        _dialogs = dialogs;
        _shell = shell;
        _changed = changed;
        _status = AppStatus.NotInstalled(false, null);
    }

    public AppDefinition Definition { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(State), nameof(StatusText), nameof(IsInstalled), nameof(CanLaunch),
        nameof(CanInstall), nameof(CanUpdate), nameof(CanUninstall), nameof(CanRepair), nameof(CanOpenFolder),
        nameof(IsRunning), nameof(HintText))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand), nameof(InstallCommand), nameof(UpdateCommand),
        nameof(UninstallCommand), nameof(RepairCommand), nameof(OpenFolderCommand))]
    private AppStatus _status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunch), nameof(CanInstall), nameof(CanUpdate), nameof(CanUninstall),
        nameof(CanRepair), nameof(CanOpenFolder), nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand), nameof(InstallCommand), nameof(UpdateCommand),
        nameof(UninstallCommand), nameof(RepairCommand), nameof(OpenFolderCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = string.Empty;

    public bool IsIdle => !IsBusy;

    public AppState State => Status.IsInstalled ? AppState.Installed : Status.CanLaunch ? AppState.Portable : AppState.NotInstalled;

    public bool IsInstalled => Status.IsInstalled;

    public bool IsRunning => Status.IsRunning;

    public string StatusText => State switch
    {
        AppState.Installed => Status.Version is { } v ? $"Telepítve · v{VersionText.Display(v)}" : "Telepítve",
        AppState.Portable => "Hordozható példány",
        _ => "Nincs telepítve",
    };

    /// <summary>Extra line under the buttons (update available / installer missing).</summary>
    public string? HintText => CanUpdateCore
        ? $"Új verzió érhető el: v{VersionText.Display(Status.InstallerVersion)}"
        : !Status.IsInstalled && !Status.IsInstallerAvailable
            ? "A telepítő nem található az openLauncher Installers mappájában."
            : null;

    private bool CanUpdateCore => Status.IsInstalled && Status.IsInstallerAvailable && VersionText.IsNewer(Status.InstallerVersion, Status.Version);

    public bool CanLaunch => !IsBusy && Status.CanLaunch;
    public bool CanInstall => !IsBusy && !Status.IsInstalled && Status.IsInstallerAvailable;
    public bool CanUpdate => !IsBusy && CanUpdateCore;
    public bool CanRepair => !IsBusy && Status.IsInstalled && Status.IsInstallerAvailable;
    public bool CanUninstall => !IsBusy && Status.IsInstalled && Status.UninstallerPath is not null;
    public bool CanOpenFolder => !IsBusy && Status.InstallLocation is not null;

    public void Refresh() => Status = _installer.GetStatus(Definition);

    [RelayCommand(CanExecute = nameof(CanLaunch))]
    private void Launch()
    {
        var result = _installer.Launch(Definition);
        if (!result.Success) _dialogs.ShowError($"{Definition.Name} nem indítható", result.Error ?? string.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private Task Install() => RunSetupAsync("Telepítés…");

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private Task Update() => RunSetupAsync("Frissítés…");

    [RelayCommand(CanExecute = nameof(CanRepair))]
    private async Task Repair()
    {
        if (!_dialogs.Confirm("Újratelepítés", $"A(z) {Definition.Name} fájljai újra felkerülnek. Az adataid megmaradnak.", "Újratelepítés"))
            return;
        await RunSetupAsync("Újratelepítés…");
    }

    [RelayCommand(CanExecute = nameof(CanUninstall))]
    private async Task Uninstall()
    {
        if (!_dialogs.Confirm($"{Definition.Name} eltávolítása",
                "Az alkalmazás eltávolításra kerül erről a gépről.\nA jegyzeteid, eseményeid és tábláid (felhasználói adatok) megmaradnak, egy újratelepítés után újra elérhetők.",
                "Eltávolítás", destructive: true))
            return;
        if (!EnsureStopped()) return;

        await RunAsync("Eltávolítás…", () => _installer.UninstallAsync(Definition), $"{Definition.Name} eltávolítása nem sikerült");
    }

    [RelayCommand(CanExecute = nameof(CanOpenFolder))]
    private void OpenFolder()
    {
        if (Status.InstallLocation is { } folder && !_shell.OpenFolder(folder))
            _dialogs.ShowError("A mappa nem nyitható meg", folder);
    }

    /// <summary>Install, update and repair all run the bundled setup (Inno upgrades in place).</summary>
    private async Task RunSetupAsync(string busyText)
    {
        if (!EnsureStopped()) return;
        await RunAsync(busyText, () => _installer.InstallAsync(Definition), $"{Definition.Name} telepítése nem sikerült");
    }

    /// <summary>Setups cannot replace / remove files of a running app: offer to close it first.</summary>
    private bool EnsureStopped()
    {
        Refresh();
        if (!Status.IsRunning) return true;

        if (!_dialogs.Confirm($"{Definition.Name} fut",
                $"A művelethez be kell zárni a(z) {Definition.Name} alkalmazást. Az adatok minden változáskor mentésre kerülnek.\n\nBezárjuk most?",
                "Bezárás és folytatás"))
            return false;

        if (_installer.StopRunning(Definition)) return true;
        _dialogs.ShowError("Nem sikerült bezárni", $"Zárd be kézzel a(z) {Definition.Name} alkalmazást, majd próbáld újra.");
        return false;
    }

    private async Task RunAsync(string busyText, Func<Task<OperationResult>> operation, string errorTitle)
    {
        BusyText = busyText;
        IsBusy = true;
        try
        {
            var result = await operation();
            if (!result.Success) _dialogs.ShowError(errorTitle, result.Error ?? string.Empty);
        }
        finally
        {
            IsBusy = false;
            BusyText = string.Empty;
            Refresh();
            _changed();
        }
    }
}
