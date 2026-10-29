using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using OpenLauncher.Core.Abstractions;
using OpenLauncher.Infrastructure;
using OpenLauncher.Infrastructure.Installation;
using OpenLauncher.Infrastructure.Platform;
using OpenLauncher.Infrastructure.Storage;
using OpenLauncher.UI.Services;
using OpenLauncher.UI.ViewModels;
using OpenLauncher.UI.Views;

namespace OpenLauncher;

/// <summary>Composition root. To add an app to the suite, only <see cref="AppCatalog"/> needs to change.</summary>
public partial class App : Application
{
    private readonly CancellationTokenSource _shutdown = new();
    private ServiceProvider? _services;
    private SingleInstanceGuard? _instanceGuard;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Keep every window inside the screen at any display scaling (125 %, 150 %, 200 % …).
        WindowStyling.EnableScreenFitting();

        _instanceGuard = SingleInstanceGuard.TryAcquire();
        if (_instanceGuard is null)
        {
            // Already running: the first instance just comes to the front.
            SingleInstanceGuard.SendToPrimary(e.Args);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        _services = ConfigureServices();

        var window = _services.GetRequiredService<MainWindow>();
        MainWindow = window;
        _services.GetRequiredService<MainViewModel>().Refresh();
        window.Show();

        _instanceGuard.StartListening(_ => Dispatcher.InvokeAsync(() => WindowStyling.BringToFront(window)), _shutdown.Token);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shutdown.Cancel();
        if (_services is not null)
        {
            try
            {
                _services.GetRequiredService<ISettingsStore>().Save();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                WriteLog(ex);
            }
            _services.Dispose();
        }
        _instanceGuard?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Infrastructure
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<IAppCatalog, AppCatalog>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IInstallationService, InnoSetupInstallationService>();

        // UI
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IShellService, ShellService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteLog(e.Exception);
        e.Handled = true;
        _services?.GetService<IDialogService>()?.ShowError("Váratlan hiba",
            $"{e.Exception.Message}\n\nA részletek a naplóban: {_services?.GetService<IAppPaths>()?.LogDirectory}");
    }

    private void WriteLog(Exception exception)
    {
        try
        {
            var directory = _services?.GetService<IAppPaths>()?.LogDirectory ?? Path.GetTempPath();
            var entry = new StringBuilder()
                .AppendLine($"[{DateTimeOffset.Now:O}] openLauncher {AppInfo.Version}")
                .AppendLine(exception.ToString())
                .AppendLine();
            File.AppendAllText(Path.Combine(directory, "error.log"), entry.ToString());
        }
        catch (IOException)
        {
            // Logging must never crash the app.
        }
    }
}
