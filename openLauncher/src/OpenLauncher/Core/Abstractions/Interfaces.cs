using OpenLauncher.Core.Models;

namespace OpenLauncher.Core.Abstractions;

public interface IAppCatalog
{
    IReadOnlyList<AppDefinition> Apps { get; }
}

public interface IAppPaths
{
    /// <summary>Folder with the bundled setups (next to openLauncher.exe).</summary>
    string InstallersDirectory { get; }

    string LogDirectory { get; }

    string SettingsFile { get; }
}

public interface ISettingsStore
{
    AppSettings Current { get; }

    void Save();
}

/// <summary>Detects, installs, uninstalls and starts the suite apps.</summary>
public interface IInstallationService
{
    AppStatus GetStatus(AppDefinition app);

    Task<OperationResult> InstallAsync(AppDefinition app, CancellationToken cancellationToken = default);

    Task<OperationResult> UninstallAsync(AppDefinition app, CancellationToken cancellationToken = default);

    OperationResult Launch(AppDefinition app);

    /// <summary>Closes running instances (needed before uninstall). Returns false if some could not be stopped.</summary>
    bool StopRunning(AppDefinition app);
}
