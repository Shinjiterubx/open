using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using OpenNotes.Core.Abstractions;
using OpenNotes.Infrastructure;
using OpenNotes.Infrastructure.Exchange;
using OpenNotes.Infrastructure.Hotkeys;
using OpenNotes.Infrastructure.Platform;
using OpenNotes.Infrastructure.Rendering;
using OpenNotes.Infrastructure.Storage;
using OpenNotes.UI.Controls;
using OpenNotes.UI.Services;
using OpenNotes.UI.ViewModels;
using OpenNotes.UI.Views;

namespace OpenNotes;

/// <summary>Composition root and application lifetime.</summary>
public partial class App : Application, IAppLifetime
{
    private const string NewNoteArgument = "--new";
    private const string MiniArgument = "--mini";

    /// <summary><c>--open-note {id}</c>: select a note (used by openCalendar to open linked notes).</summary>
    private const string OpenNoteArgument = "--open-note";

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
            // Already running: hand our arguments (e.g. a double-clicked .openNote file) to that instance.
            SingleInstanceGuard.SendToPrimary(e.Args);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        _services = ConfigureServices();
        var paths = _services.GetRequiredService<IAppPaths>();
        PreviewEnvironment.Configure(paths);
        AppRegistration.Publish(paths);
        _services.GetRequiredService<IPreviewAssetCache>().Reset();

        var settings = _services.GetRequiredService<ISettingsStore>();
        _services.GetRequiredService<MainViewModel>().Initialize();

        var tray = _services.GetRequiredService<TrayIcon>();
        tray.Show();

        var hotkeyFailures = _services.GetRequiredService<AppHotkeys>().Apply();
        if (hotkeyFailures.Count > 0)
            tray.Notify("Foglalt gyorsbillentyű", "Másik program használja: " + string.Join(", ", hotkeyFailures) + ". Állíts be másikat a Beállításokban.");

        _instanceGuard.StartListening(
            args => Dispatcher.InvokeAsync(() => HandleArguments(args, fromAnotherInstance: true)),
            _shutdown.Token);

        var startInBackground = e.Args.Contains(RegistryAutoStartService.BackgroundArgument) || settings.Current.StartMinimized;
        var handled = HandleArguments(e.Args, fromAnotherInstance: false);
        if (!handled && !startInBackground)
            _services.GetRequiredService<IWindowService>().ShowLastUsed();

        // First start of the day → "remaining tasks" summary; afterwards a daily check while running.
        _services.GetRequiredService<ReminderScheduler>().Start();
    }

    public void RequestExit()
    {
        if (IsExiting) return;
        IsExiting = true;

        if (_services is not null)
        {
            _services.GetRequiredService<MainViewModel>().Editor.Flush();
            // Only windows that were actually created (Windows contains hidden ones too).
            foreach (var window in Windows.OfType<MainWindow>()) window.SaveLayout();
            foreach (var window in Windows.OfType<MiniWindow>()) window.SaveLayout();
            TrySaveSettings();
        }

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true;
        _shutdown.Cancel();

        if (_services is not null)
        {
            _services.GetRequiredService<MainViewModel>().Editor.Flush();
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
        var main = _services.GetRequiredService<MainViewModel>();

        var openNoteIndex = args.ToList().IndexOf(OpenNoteArgument);
        if (openNoteIndex >= 0 && openNoteIndex + 1 < args.Count && Guid.TryParse(args[openNoteIndex + 1], out var noteId))
        {
            if (!main.OpenNoteById(noteId))
                _services.GetRequiredService<IDialogService>().ShowError("Jegyzet nem található",
                    "A hivatkozott jegyzet nincs meg az openNotes könyvtárban (lehet, hogy törölték).");
            windows.ShowMain();
            return true;
        }

        var files = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal) && File.Exists(a)).ToList();
        if (files.Count > 0)
        {
            main.ImportFiles(files);
            windows.ShowMain();
            return true;
        }

        if (args.Contains(NewNoteArgument))
        {
            main.NewNote();
            windows.ShowQuickWindow(focusTitle: true);
            return true;
        }

        if (args.Contains(MiniArgument))
        {
            main.OpenLastNote();
            windows.ShowMini();
            return true;
        }

        if (fromAnotherInstance)
        {
            windows.ShowLastUsed();
            return true;
        }

        return false;
    }

    private ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Application
        services.AddSingleton<IAppLifetime>(this);

        // Infrastructure
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<INoteSerializer, OpenNoteSerializer>();
        services.AddSingleton<INoteRepository, FileNoteRepository>();
        services.AddSingleton<IFolderRepository, JsonFolderRepository>();
        services.AddSingleton<OpenNoteFileFormat>();
        services.AddSingleton<MarkdownFileFormat>();
        services.AddSingleton<INoteImporter>(sp => sp.GetRequiredService<OpenNoteFileFormat>());
        services.AddSingleton<INoteImporter>(sp => sp.GetRequiredService<MarkdownFileFormat>());
        services.AddSingleton<INoteExporter>(sp => sp.GetRequiredService<OpenNoteFileFormat>());
        services.AddSingleton<INoteExporter>(sp => sp.GetRequiredService<MarkdownFileFormat>());
        services.AddSingleton<NoteImportService>();
        services.AddSingleton<IMarkdownRenderer, MarkdigRenderer>();
        services.AddSingleton<IPreviewAssetCache, PreviewAssetCache>();
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<IAutoStartService, RegistryAutoStartService>();

        // UI services
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<AppHotkeys>();
        services.AddSingleton<TrayIcon>();
        services.AddSingleton<ReminderScheduler>();

        // View models
        services.AddSingleton<NoteEditorViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MiniWindow>();
        services.AddSingleton<ReminderSummaryWindow>();
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
                .AppendLine($"[{DateTimeOffset.Now:O}] openNotes {AppInfo.Version}")
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
