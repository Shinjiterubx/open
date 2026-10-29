using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using OpenCalendar.Core.Abstractions;
using OpenCalendar.Core.Reminders;
using OpenCalendar.Infrastructure;
using OpenCalendar.Infrastructure.Integration;
using OpenCalendar.Infrastructure.Platform;
using OpenCalendar.Infrastructure.Storage;
using OpenCalendar.Services;
using OpenCalendar.UI.Services;
using OpenCalendar.UI.ViewModels;
using OpenCalendar.UI.Views;

namespace OpenCalendar;

/// <summary>Composition root and application lifetime.</summary>
public partial class App : Application, IAppLifetime
{
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
        PublishLocation();

        var main = _services.GetRequiredService<MainViewModel>();
        main.Initialize();

        _services.GetRequiredService<TrayIcon>().Show();

        var toasts = _services.GetRequiredService<ToastManager>();
        toasts.OpenRequested = alert => OpenAlert(main, alert);
        _services.GetRequiredService<ReminderService>().Start();

        _instanceGuard.StartListening(args => Dispatcher.InvokeAsync(() => HandleArguments(args, fromAnotherInstance: true)), _shutdown.Token);

        var settings = _services.GetRequiredService<ISettingsStore>();
        var inBackground = e.Args.Contains(RegistryAutoStartService.BackgroundArgument) || settings.Current.StartMinimized;
        if (!HandleArguments(e.Args, fromAnotherInstance: false) && !inBackground)
            _services.GetRequiredService<IWindowService>().ShowMain();
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

    /// <returns><c>true</c> if the arguments already opened a window.</returns>
    private bool HandleArguments(IReadOnlyList<string> args, bool fromAnotherInstance)
    {
        if (_services is null) return false;

        var windows = _services.GetRequiredService<IWindowService>();

        // --goto yyyy-MM-dd : show that day (used by openKanban)
        var gotoIndex = args.ToList().IndexOf("--goto");
        if (gotoIndex >= 0 && gotoIndex + 1 < args.Count && DateOnly.TryParseExact(args[gotoIndex + 1], "yyyy-MM-dd", out var date))
        {
            _services.GetRequiredService<MainViewModel>().GoToDate(date);
            windows.ShowMain();
            return true;
        }

        var files = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal) && File.Exists(a)).ToList();
        if (files.Count > 0)
        {
            windows.ShowMain();
            _services.GetRequiredService<MainViewModel>().ImportFiles(files);
            return true;
        }

        if (fromAnotherInstance)
        {
            windows.ShowMain();
            return true;
        }
        return false;
    }

    private void OpenAlert(MainViewModel main, ReminderAlert alert)
    {
        if (alert.EventId is { } eventId)
        {
            _services?.GetRequiredService<IWindowService>().ShowMain();
            main.OpenEvent(eventId, DateOnly.FromDateTime(alert.OccurrenceStart));
        }
        else
        {
            main.OpenNote(alert.NoteId);
        }
    }

    /// <summary>Lets openKanban / openLauncher find this app (HKCU\Software\openCalendar).</summary>
    private static void PublishLocation()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\openCalendar", writable: true);
            if (Environment.ProcessPath is { } exe) key.SetValue("ExePath", exe);
            key.SetValue("Version", AppInfo.Version);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Integration is optional.
        }
    }

    private ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IAppLifetime>(this);

        // Infrastructure
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<ICalendarSerializer, OpenCalendarSerializer>();
        services.AddSingleton<ICalendarRepository, FileCalendarRepository>();
        services.AddSingleton<INoteSource, OpenNotesLibrary>();
        services.AddSingleton<INoteLauncher, OpenNotesLauncher>();
        services.AddSingleton<IKanbanSource, OpenKanbanFeed>();
        services.AddSingleton<IKanbanLauncher, OpenKanbanLauncher>();
        services.AddSingleton<IAutoStartService, RegistryAutoStartService>();

        // Application
        services.AddSingleton<CalendarService>();

        // UI services
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<ToastManager>();
        services.AddSingleton<ReminderService>();
        services.AddSingleton<TrayIcon>();

        // View models & views
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
                .AppendLine($"[{DateTimeOffset.Now:O}] openCalendar {AppInfo.Version}")
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
