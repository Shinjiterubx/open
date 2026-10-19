using OpenLauncher.Core.Abstractions;

namespace OpenLauncher.Infrastructure;

public sealed class AppPaths : IAppPaths
{
    public AppPaths()
    {
        InstallersDirectory = Path.Combine(AppContext.BaseDirectory, "Installers");
        var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "openLauncher");
        LogDirectory = Path.Combine(dataDirectory, "Logs");
        SettingsFile = Path.Combine(dataDirectory, "settings.json");
        Directory.CreateDirectory(LogDirectory);
    }

    public string InstallersDirectory { get; }
    public string LogDirectory { get; }
    public string SettingsFile { get; }
}
