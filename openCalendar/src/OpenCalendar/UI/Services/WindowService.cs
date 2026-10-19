using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OpenCalendar.UI.ViewModels;
using OpenCalendar.UI.Views;

namespace OpenCalendar.UI.Services;

/// <summary>Creates windows through DI and shows dialogs owned by the active window.</summary>
public sealed class WindowService : IWindowService
{
    private readonly IServiceProvider _services;

    public WindowService(IServiceProvider services) => _services = services;

    private MainWindow Main => _services.GetRequiredService<MainWindow>();

    public void ShowMain() => WindowStyling.BringToFront(Main);

    public bool ShowEventEditor(EventEditorViewModel editor)
    {
        var window = new EventEditorWindow(editor);
        SetOwner(window);
        window.ShowDialog();
        return editor.Outcome != EditorOutcome.Cancelled;
    }

    public bool ShowCalendarDialog(CalendarDialogViewModel dialog)
    {
        var window = new CalendarDialog(dialog);
        SetOwner(window);
        return window.ShowDialog() == true;
    }

    public void ShowSettings()
    {
        var window = _services.GetRequiredService<SettingsWindow>();
        SetOwner(window);
        window.ShowDialog();
    }

    private void SetOwner(Window window)
    {
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w != window && w is not ToastWindow);
        if (owner is null && Main.IsVisible) owner = Main;

        if (owner is not null) window.Owner = owner;
        else window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }
}
