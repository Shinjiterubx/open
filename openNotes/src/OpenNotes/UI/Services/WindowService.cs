using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Models;
using OpenNotes.UI.ViewModels;
using OpenNotes.UI.Views;

namespace OpenNotes.UI.Services;

/// <summary>Creates windows lazily through DI and decides which one to show.</summary>
public sealed class WindowService : IWindowService
{
    private readonly IServiceProvider _services;
    private readonly ISettingsStore _settings;

    public WindowService(IServiceProvider services, ISettingsStore settings)
    {
        _services = services;
        _settings = settings;
    }

    private MainWindow Main => _services.GetRequiredService<MainWindow>();
    private MiniWindow Mini => _services.GetRequiredService<MiniWindow>();

    public void ShowMain()
    {
        // Opening a note from the summary: step the (topmost) summary out of the way.
        foreach (var summary in Application.Current.Windows.OfType<ReminderSummaryWindow>().Where(w => w.IsActive))
            summary.Hide();

        RememberWindow(AppWindow.Main);
        BringMainToFront();
    }

    public void ShowMini()
    {
        RememberWindow(AppWindow.Mini);
        Mini.ShowAndFocus(focusTitle: false);
    }

    public void ShowLastUsed()
    {
        if (_settings.Current.LastUsedWindow != AppWindow.Mini)
        {
            ShowMain();
            return;
        }

        // Resolved lazily: MainViewModel itself depends on IWindowService.
        _services.GetRequiredService<MainViewModel>().OpenLastNote();
        if (Main.IsVisible) Main.Hide();
        ShowMini();
    }

    public void ShowQuickWindow(bool focusTitle = false)
    {
        // Hotkeys don't change the remembered window: a quick note is not a mode switch.
        if (_settings.Current.HotkeyTarget == HotkeyTarget.MainWindow)
        {
            BringMainToFront();
            if (focusTitle) Main.FocusTitle();
        }
        else
        {
            Mini.ShowAndFocus(focusTitle);
        }
    }

    public bool HideQuickWindowIfActive()
    {
        Window quick = _settings.Current.HotkeyTarget == HotkeyTarget.MainWindow ? Main : Mini;
        if (!quick.IsVisible || !quick.IsActive) return false;

        if (quick is MainWindow) quick.WindowState = WindowState.Minimized;
        else quick.Hide();
        return true;
    }

    public void ToggleMiniMode()
    {
        var mini = Mini;
        var main = Main;

        if (mini.IsVisible && mini.IsActive)
        {
            mini.Hide();
            ShowMain();
        }
        else
        {
            main.SaveLayout();
            main.Hide();
            ShowMini();
        }
    }

    public void ShowSettings()
    {
        var window = _services.GetRequiredService<SettingsWindow>();
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w is not SettingsWindow)
                    ?? (Main.IsVisible ? Main : null);

        if (owner is not null) window.Owner = owner;
        else window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        window.ShowDialog();
    }

    public void ShowReminderSummary()
    {
        var summary = _services.GetRequiredService<ReminderSummaryWindow>();
        summary.ShowAndActivate();
    }

    public void ShowImage(byte[] data, string fileName)
    {
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? (Mini.IsVisible ? Mini : Main);
        var viewer = ImageViewerWindow.TryCreate(data, fileName, owner);
        viewer?.ShowDialog();
    }

    private void BringMainToFront()
    {
        var main = Main;
        WindowStyling.BringToFront(main);
        main.FocusEditor();
    }

    private void RememberWindow(AppWindow window)
    {
        if (_settings.Current.LastUsedWindow == window) return;
        _settings.Current.LastUsedWindow = window;
        try { _settings.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* saved again on exit */ }
    }

    public void FocusTitle()
    {
        if (Mini.IsVisible && Mini.IsActive) Mini.FocusTitle();
        else Main.FocusTitle();
    }
}
