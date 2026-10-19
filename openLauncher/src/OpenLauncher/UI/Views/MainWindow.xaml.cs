using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using OpenLauncher.Core.Abstractions;
using OpenLauncher.UI.Services;
using OpenLauncher.UI.ViewModels;

namespace OpenLauncher.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsStore _settings;
    private readonly IDialogService _dialogs;

    public MainWindow(MainViewModel viewModel, ISettingsStore settings, IDialogService dialogs)
    {
        _dialogs = dialogs;
        InitializeComponent();
        _viewModel = viewModel;
        _settings = settings;
        DataContext = viewModel;

        WindowStyling.ApplyRoundedCorners(this);
        WindowStyling.KeepContentOnScreenWhenMaximized(this, RootGrid);
        WindowStyling.RestoreBounds(this, settings.Current.MainWindowBounds);

        StateChanged += (_, _) => UpdateMaximizeGlyph();
        // Apps may have been installed / closed elsewhere meanwhile: re-check when the user comes back.
        Activated += (_, _) => _viewModel.Refresh();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_viewModel.Apps.Any(a => a.IsBusy))
        {
            // A setup is still running – closing now would hide its result.
            e.Cancel = true;
            _dialogs.ShowError("Művelet folyamatban", "Várd meg, amíg a telepítés / eltávolítás befejeződik.");
            return;
        }
        _settings.Current.MainWindowBounds = WindowStyling.CaptureBounds(this);
        base.OnClosing(e);
    }

    /// <summary>ContextMenus live outside the visual tree: hand them the card's DataContext explicitly.</summary>
    private void OnCardMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.DataContext = button.DataContext;
        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void UpdateMaximizeGlyph()
    {
        var maximized = WindowState == WindowState.Maximized;
        MaximizeButton.Content = maximized ? "" : "";
        MaximizeButton.ToolTip = maximized ? "Előző méret" : "Teljes méret";
    }
}
