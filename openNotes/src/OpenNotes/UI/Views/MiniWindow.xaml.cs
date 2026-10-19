using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using OpenNotes.Core.Abstractions;
using OpenNotes.UI.Services;
using OpenNotes.UI.ViewModels;

namespace OpenNotes.UI.Views;

/// <summary>Compact, always-on-top quick note window. Closing only hides it.</summary>
public partial class MiniWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsStore _settings;
    private readonly IAppLifetime _lifetime;
    private bool _positioned;

    public MiniWindow(MainViewModel viewModel, ISettingsStore settings, IAppLifetime lifetime)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settings = settings;
        _lifetime = lifetime;
        DataContext = viewModel;

        WindowStyling.ApplyRoundedCorners(this);
        Deactivated += (_, _) => _viewModel.Editor.Flush();
        PreviewKeyDown += OnWindowPreviewKeyDown;
        TitleBox.KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Down)) return;
            NoteEditor.FocusEditor();
            e.Handled = true;
        };
    }

    public void ShowAndFocus(bool focusTitle)
    {
        EnsurePosition();
        WindowStyling.BringToFront(this);

        Dispatcher.InvokeAsync(() =>
        {
            if (focusTitle || (string.IsNullOrWhiteSpace(_viewModel.Editor.Title) && string.IsNullOrEmpty(_viewModel.Editor.Content)))
            {
                TitleBox.Focus();
                TitleBox.CaretIndex = TitleBox.Text.Length;
            }
            else
            {
                NoteEditor.FocusEditor(moveCaretToEnd: true);
            }
        }, DispatcherPriority.Input);
    }

    public void FocusTitle() => Dispatcher.InvokeAsync(() => TitleBox.Focus(), DispatcherPriority.Input);

    public void SaveLayout()
    {
        if (_positioned) _settings.Current.MiniWindowBounds = WindowStyling.CaptureBounds(this);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.Editor.Flush();
        SaveLayout();

        if (!_lifetime.IsExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    /// <summary>First show: restore the saved position, or dock to the bottom-right of the work area.</summary>
    private void EnsurePosition()
    {
        if (_positioned) return;
        _positioned = true;

        var saved = _settings.Current.MiniWindowBounds;
        if (saved is not null)
        {
            saved.IsMaximized = false;
            if (WindowStyling.RestoreBounds(this, saved)) return;
        }

        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 24;
        Top = area.Bottom - Height - 24;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        Hide();
        _viewModel.Editor.Flush();
        e.Handled = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
