using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using OpenKanban.Core.Abstractions;
using OpenKanban.Infrastructure;
using OpenKanban.Infrastructure.Integration;
using OpenKanban.Infrastructure.Platform;
using OpenKanban.Infrastructure.Storage;
using OpenKanban.Services;
using OpenKanban.UI.Services;
using OpenKanban.UI.ViewModels;
using OpenKanban.UI.Views;

namespace OpenKanban;

/// <summary>Composition root and application lifetime.</summary>
public partial class App : Application, IAppLifetime
{
    /// <summary><c>--open-card {id}</c>: open a card (used by openCalendar).</summary>
    private const string OpenCardArgument = "--open-card";

    private readonly CancellationTokenSource _shutdown = new();
    private ServiceProvider? _services;
    private SingleInstanceGuard? _instanceGuard;

    public bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Keep every window inside the screen at any display scaling (125 %, 150 %, 200 % …).
        WindowStyling.EnableScreenFitting();

        _instanceGuard = SingleInstanceGuard.TryAcquire();
        if (_instanceGuard is null)
        {
            SingleInstanceGuard.SendToPrimary(e.Args);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        _services = ConfigureServices();

        var paths = _services.GetRequiredService<IAppPaths>();
        AppRegistry.Publish(AppInfo.Name, new Dictionary<string, string>
        {
            ["ExePath"] = Environment.ProcessPath ?? string.Empty,
            ["DataDirectory"] = paths.WorkspacesDirectory,
            ["DeadlinesFile"] = paths.DeadlinesFile,
            ["Version"] = AppInfo.Version,
        });

        _services.GetRequiredService<MainViewModel>().Initialize();
        _instanceGuard.StartListening(args => Dispatcher.InvokeAsync(() => HandleArguments(args)), _shutdown.Token);

        _services.GetRequiredService<IWindowService>().ShowMain();
        // Let the window render before a possible modal card editor opens.
        Dispatcher.InvokeAsync(() => HandleArguments(e.Args), DispatcherPriority.ApplicationIdle);
    }

    public void RequestExit()
    {
        if (IsExiting) return;
        IsExiting = true;
        foreach (var window in Windows.OfType<MainWindow>()) window.SaveLayout();
        TrySaveSettings();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true;
        _shutdown.Cancel();
        if (_services is not null)
        {
            TrySaveSettings();
            _services.Dispose();
        }
        _instanceGuard?.Dispose();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        RequestExit();
        base.OnSessionEnding(e);
    }

    private void HandleArguments(IReadOnlyList<string> args)
    {
        if (_services is null) return;
        var main = _services.GetRequiredService<MainViewModel>();
        _services.GetRequiredService<IWindowService>().ShowMain();

        var index = args.ToList().IndexOf(OpenCardArgument);
        if (index >= 0 && index + 1 < args.Count && Guid.TryParse(args[index + 1], out var cardId))
        {
            if (!main.OpenCardById(cardId))
                _services.GetRequiredService<IDialogService>().ShowError("Kártya nem található", "A hivatkozott kártya időközben törlődött.");
            return;
        }

        var files = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal) && File.Exists(a)).ToList();
        if (files.Count > 0) main.ImportFiles(files);
    }

    private ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IAppLifetime>(this);

        // Infrastructure
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IWorkspaceSerializer, OpenKanbanSerializer>();
        services.AddSingleton<IWorkspaceRepository, FileWorkspaceRepository>();
        services.AddSingleton<IDeadlineFeed, DeadlineFeedWriter>();
        services.AddSingleton<INoteSource, OpenNotesSource>();
        services.AddSingleton<ICompanionLauncher, CompanionLauncher>();

        // Application
        services.AddSingleton<KanbanService>();

        // UI
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddTransient<SettingsWindow>();

        return services.BuildServiceProvider();
    }

    private void TrySaveSettings()
    {
        try
        {
            _services?.GetRequiredService<ISettingsStore>().Save();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            WriteLog(ex);
        }
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
                .AppendLine($"[{DateTimeOffset.Now:O}] openKanban {AppInfo.Version}")
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
