using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using OpenCalendar.Core.Abstractions;
using OpenCalendar.Infrastructure.Storage;
using OpenCalendar.UI.Services;
using OpenCalendar.UI.ViewModels;

namespace OpenCalendar.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsStore _settings;
    private readonly IAppLifetime _lifetime;

    public MainWindow(MainViewModel viewModel, ISettingsStore settings, IAppLifetime lifetime)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settings = settings;
        _lifetime = lifetime;
        DataContext = viewModel;

        WindowStyling.ApplyRoundedCorners(this);
        WindowStyling.KeepContentOnScreenWhenMaximized(this, RootGrid);
        WindowStyling.RestoreBounds(this, settings.Current.MainWindowBounds);

        StateChanged += (_, _) => UpdateMaximizeGlyph();
        DragOver += OnWindowDragOver;
        Drop += OnWindowDrop;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveLayout();

        if (!_lifetime.IsExiting && _settings.Current.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        if (!_lifetime.IsExiting)
        {
            e.Cancel = true;
            _lifetime.RequestExit();
            return;
        }

        base.OnClosing(e);
    }

    public void SaveLayout()
    {
        if (!IsLoaded) return;
        _settings.Current.MainWindowBounds = WindowStyling.CaptureBounds(this);
    }

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (MoreButton.ContextMenu is not { } menu) return;
        menu.DataContext = DataContext;
        menu.PlacementTarget = MoreButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private static IReadOnlyList<string> CalendarFiles(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] files
            ? files.Where(f => f.EndsWith(OpenCalendarSerializer.FileExtension, StringComparison.OrdinalIgnoreCase)).ToList()
            : Array.Empty<string>();

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = CalendarFiles(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        var files = CalendarFiles(e);
        if (files.Count > 0) _viewModel.ImportFiles(files);
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
