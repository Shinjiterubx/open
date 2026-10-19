using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using OpenKanban.Core.Abstractions;
using OpenKanban.Infrastructure.Storage;
using OpenKanban.UI.Services;
using OpenKanban.UI.ViewModels;

namespace OpenKanban.UI.Views;

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
        SidebarColumn.Width = new GridLength(Math.Clamp(settings.Current.SidebarWidth, 200, 420));

        StateChanged += (_, _) => UpdateMaximizeGlyph();
        PreviewKeyDown += OnWindowPreviewKeyDown;
        DragOver += OnWindowDragOver;
        Drop += OnWindowDrop;
    }

    /// <summary>openKanban has no tray icon: closing the window exits the app.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        SaveLayout();
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
        if (SidebarColumn.ActualWidth > 0) _settings.Current.SidebarWidth = SidebarColumn.ActualWidth;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && _viewModel.IsBoardOpen)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SearchBox.IsKeyboardFocusWithin)
        {
            _viewModel.SearchText = string.Empty;
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        if (MoreButton.ContextMenu is not { } menu) return;
        menu.DataContext = DataContext;
        menu.PlacementTarget = MoreButton;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private static IReadOnlyList<string> WorkspaceFiles(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] files
            ? files.Where(f => f.EndsWith(OpenKanbanSerializer.FileExtension, StringComparison.OrdinalIgnoreCase)).ToList()
            : Array.Empty<string>();

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;   // card / column drags are handled by the board
        e.Effects = WorkspaceFiles(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        var files = WorkspaceFiles(e);
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
