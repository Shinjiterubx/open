namespace OpenLauncher.Core.Models;

/// <summary>
/// One app of the suite. To add an app: add an entry to <c>AppCatalog</c>, put its icon in Assets/Apps
/// and its installer into the launcher's Installers folder (see build-all.ps1).
/// </summary>
public sealed record AppDefinition(
    string Id,
    string Name,
    string Tagline,
    string Description,
    string AccentColor,
    string ExeName,
    /// <summary>Inno Setup AppId incl. braces; the uninstall entry is <c>{AppId}_is1</c>.</summary>
    string InnoAppId,
    /// <summary>File name of the setup in the Installers folder, e.g. "openNotes-Setup.exe".</summary>
    string InstallerFileName)
{
    public string IconUri => $"pack://application:,,,/Assets/Apps/{Id}.png";

    /// <summary>The app publishes its location here (HKCU\Software\{Id}) – used for portable copies.</summary>
    public string RegistryKey => $@"Software\{Id}";
}

/// <summary>Detected state of an app on this machine.</summary>
public sealed record AppStatus(
    bool IsInstalled,
    string? Version,
    string? InstallLocation,
    string? ExePath,
    string? UninstallerPath,
    bool IsRunning,
    bool IsInstallerAvailable,
    string? InstallerVersion)
{
    public bool CanLaunch => ExePath is not null;

    public static AppStatus NotInstalled(bool installerAvailable, string? installerVersion) =>
        new(false, null, null, null, null, false, installerAvailable, installerVersion);
}

public sealed record OperationResult(bool Success, string? Error = null)
{
    public static OperationResult Ok { get; } = new(true);

    public static OperationResult Fail(string error) => new(false, error);
}
