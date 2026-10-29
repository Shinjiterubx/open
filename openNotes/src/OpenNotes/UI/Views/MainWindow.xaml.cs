using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        Sidebar.SizeChanged += (_, _) => FitSidebarPanels();
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
        if (e.Key == Key.Escape && _viewModel.IsCardView)
        {
            _viewModel.IsCardView = false;
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control && !_viewModel.IsCardView)
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

    /// <summary>
    /// The reminder and folder panels share the sidebar with the note list: on short windows (small screens,
    /// 150 %+ scaling) they get less room, so the note list always stays usable.
    /// </summary>
    private void FitSidebarPanels()
    {
        var height = Sidebar.ActualHeight;
        if (height <= 0) return;
        ReminderScroll.MaxHeight = Math.Clamp(height * 0.18, 56, 210);
        FolderList.MaxHeight = Math.Clamp(height * 0.22, 64, 232);
    }

    // ───────────────────────── folder menus ─────────────────────────

    /// <summary>Note context menu (list row or card): fills "Áthelyezés mappába" and names the close / reopen item.</summary>
    private void OnNoteMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu menu || (menu.PlacementTarget as FrameworkElement)?.DataContext is not NoteItemViewModel note) return;

        foreach (var item in menu.Items.OfType<MenuItem>())
        {
            switch (item.Uid)
            {
                case "MoveMenu":
                    FillFolderMenu(item.Items, note);
                    break;
                case "CloseMenu":
                    item.Header = note.IsDone ? "Újranyitás" : "Lezárás (Kész)";
                    item.Tag = note.IsDone ? "\uE7A7" : "\uE73E";
                    break;
            }
        }
    }

    /// <summary>The folder chip above the title: move the open note to another folder.</summary>
    private void OnFolderChipClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedNote is not { } note) return;

        var menu = new ContextMenu { PlacementTarget = FolderChip, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        FillFolderMenu(menu.Items, note);
        menu.IsOpen = true;
    }

    /// <summary>"Mappa nélkül", every folder (the current one checked), then "Új mappa…".</summary>
    private void FillFolderMenu(ItemCollection items, NoteItemViewModel note)
    {
        items.Clear();
        foreach (var target in _viewModel.MoveTargets)
        {
            var isCurrent = ReferenceEquals(note.Folder, target) || (note.Folder is null && !target.IsFolder);
            items.Add(new MenuItem
            {
                Header = FolderHeader(target),
                Tag = isCurrent ? "\uE73E" : string.Empty,
                FontWeight = isCurrent ? FontWeights.SemiBold : FontWeights.Normal,
                Command = _viewModel.MoveToFolderCommand,
                CommandParameter = new MoveRequest(note, target),
                IsEnabled = !isCurrent,
            });
        }

        items.Add(new Separator());
        items.Add(new MenuItem
        {
            Header = "Új mappa…",
            Tag = "\uE710",
            Command = _viewModel.NewFolderAndMoveCommand,
            CommandParameter = note,
        });
    }

    private static object FolderHeader(FolderNavItemViewModel target)
    {
        var color = Brushes.Gray as Brush;
        try
        {
            color = (Brush)new BrushConverter().ConvertFromString(target.Color)!;
        }
        catch (FormatException)
        {
            // keep grey
        }

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(new System.Windows.Shapes.Ellipse { Width = 9, Height = 9, Fill = color, Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(new TextBlock { Text = target.Name, VerticalAlignment = VerticalAlignment.Center });
        return panel;
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
