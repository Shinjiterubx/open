using System.Windows;
using System.Windows.Controls;
using OpenCalendar.Core.Reminders;
using OpenCalendar.UI.ViewModels;

namespace OpenCalendar.UI.Views;

/// <summary>Reminder notification card (stays until dismissed; doesn't steal focus).</summary>
public partial class ToastWindow : Window
{
    public ToastWindow(ReminderAlert alert)
    {
        InitializeComponent();
        Alert = alert;

        var minutesLeft = (int)Math.Round((alert.OccurrenceStart - DateTime.Now).TotalMinutes);
        WhenText.Text = minutesLeft switch
        {
            <= 0 when alert.IsAllDay => "MA",
            <= 0 => "MOST",
            _ => ReminderPlanner.DescribeOffset(minutesLeft).ToUpperInvariant(),
        };
        TitleText.Text = alert.Title;

        var when = alert.IsAllDay
            ? Hu.Capitalize(alert.OccurrenceStart.ToString("MMMM d., dddd", Hu.Culture)) + " · egész nap"
            : Hu.Capitalize(alert.OccurrenceStart.ToString("MMMM d., dddd · HH:mm", Hu.Culture));
        DetailText.Text = alert.EventId is null ? $"openNotes határidő · {when}" : when;
        if (!string.IsNullOrWhiteSpace(alert.Location)) DetailText.Text += $"\n📍 {alert.Location}";
        if (alert.EventId is null) Glyph.Text = "";
    }

    public ReminderAlert Alert { get; }

    public event EventHandler? OpenClicked;
    public event EventHandler<int>? SnoozeClicked;

    private void OnOpen(object sender, RoutedEventArgs e) => OpenClicked?.Invoke(this, EventArgs.Empty);

    private void OnSnooze(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && int.TryParse(tag, out var minutes)) SnoozeClicked?.Invoke(this, minutes);
    }

    private void OnDismiss(object sender, RoutedEventArgs e) => Close();
}
