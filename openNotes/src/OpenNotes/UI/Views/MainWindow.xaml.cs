using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenNotes.Core.Abstractions;
using OpenNotes.UI.Services;
using OpenNotes.UI.ViewModels;

namespace OpenNotes.UI.Views;

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
        SidebarColumn.Width = new GridLength(Math.Clamp(settings.Current.SidebarWidth, 230, 480));

        StateChanged += (_, _) => UpdateMaximizeGlyph();
        Deactivated += (_, _) => _viewModel.Editor.Flush();
        PreviewKeyDown += OnWindowPreviewKeyDown;
        DragOver += OnWindowDragOver;
        Drop += OnWindowDrop;
        TitleBox.KeyDown += OnTitleKeyDown;
        DueTimeBox.KeyDown += OnDueTimeKeyDown;
        DueCalendar.PreviewMouseUp += OnCalendarPreviewMouseUp;
        _viewModel.NoteCreated += (_, _) => { if (IsActive) FocusTitle(); };
    }

    public void FocusEditor()
    {
        if (!_viewModel.Editor.HasNote) return;
        if (string.IsNullOrWhiteSpace(_viewModel.Editor.Title)) FocusTitle();
        else Dispatcher.InvokeAsync(() => NoteEditor.FocusEditor(), System.Windows.Threading.DispatcherPriority.Input);
    }

    public void FocusTitle() =>
        Dispatcher.InvokeAsync(() =>
        {
            TitleBox.Focus();
            TitleBox.CaretIndex = TitleBox.Text.Length;
        }, System.Windows.Threading.DispatcherPriority.Input);

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.Editor.Flush();
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
        if (!IsLoaded) return;   // never shown: keep the stored layout
        _settings.Current.MainWindowBounds = WindowStyling.CaptureBounds(this);
        _settings.Current.SidebarWidth = SidebarColumn.ActualWidth > 0 ? SidebarColumn.ActualWidth : _settings.Current.SidebarWidth;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && SearchBox.IsKeyboardFocusWithin)
        {
            _viewModel.SearchText = string.Empty;
            NoteEditor.FocusEditor();
            e.Handled = true;
        }
        else if (e.Key == Key.Down && SearchBox.IsKeyboardFocusWithin)
        {
            NoteList.Focus();
            e.Handled = true;
        }
    }

    private void OnTitleKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Down)) return;
        NoteEditor.FocusEditor();
        e.Handled = true;
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (ExportButton.ContextMenu is not { } menu) return;
        menu.DataContext = DataContext;
        menu.PlacementTarget = ExportButton;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private static IReadOnlyList<string> NoteFiles(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] files ? files : Array.Empty<string>();

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = NoteFiles(e).Count > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        var files = NoteFiles(e);
        if (files.Count > 0) _viewModel.ImportFiles(files);
    }

    // ───────────────────────── due date popup ─────────────────────────

    private void OnDuePopupOpened(object? sender, EventArgs e)
    {
        DueCalendar.DisplayDate = DueCalendar.SelectedDate ?? DateTime.Today;
    }

    private void OnDuePopupClose(object sender, RoutedEventArgs e)
    {
        CommitDueTime();
        DueToggle.IsChecked = false;
    }

    private void CommitDueTime() =>
        DueTimeBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

    private void OnDueTimeKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CommitDueTime();
        e.Handled = true;
    }

    /// <summary>A Calendar in a Popup keeps mouse capture after a click, so the next click is swallowed – release it.</summary>
    private void OnCalendarPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.Captured is System.Windows.Controls.Primitives.CalendarItem or System.Windows.Controls.Primitives.CalendarDayButton)
            Mouse.Capture(null);
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
