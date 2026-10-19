using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using OpenNotes.UI.Services;
using OpenNotes.UI.ViewModels;

namespace OpenNotes.UI.Views;

/// <summary>
/// "Remaining tasks" summary: overdue, due today and upcoming notes with active reminders.
/// Shown at the first start of the day, at the daily reminder time and on demand. Closing only hides it.
/// </summary>
public partial class ReminderSummaryWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IAppLifetime _lifetime;

    public ReminderSummaryWindow(MainViewModel viewModel, IAppLifetime lifetime)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _lifetime = lifetime;
        DataContext = viewModel;

        WindowStyling.ApplyRoundedCorners(this);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            Hide();
            e.Handled = true;
        };
    }

    public void ShowAndActivate()
    {
        _viewModel.RefreshReminders();
        WindowStyling.BringToFront(this);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_lifetime.IsExiting)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Hide();
}
