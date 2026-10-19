using System.Media;
using System.Windows;
using System.Windows.Threading;
using OpenCalendar.Core.Abstractions;
using OpenCalendar.Core.Reminders;
using OpenCalendar.UI.Views;

namespace OpenCalendar.UI.Services;

/// <summary>Stacks reminder notifications in the bottom-right corner; handles open and snooze.</summary>
public sealed class ToastManager
{
    private const int MaxVisible = 4;
    private const double Gap = 10;

    private readonly ISettingsStore _settings;
    private readonly List<ToastWindow> _open = new();
    private readonly Queue<ReminderAlert> _waiting = new();

    public ToastManager(ISettingsStore settings) => _settings = settings;

    /// <summary>Invoked when the user clicks "Megnyitás" on a notification.</summary>
    public Action<ReminderAlert>? OpenRequested { get; set; }

    public void Show(ReminderAlert alert)
    {
        if (_open.Count >= MaxVisible)
        {
            _waiting.Enqueue(alert);
            return;
        }

        var toast = new ToastWindow(alert);
        toast.OpenClicked += (_, _) =>
        {
            toast.Close();
            OpenRequested?.Invoke(alert);
        };
        toast.SnoozeClicked += (_, minutes) =>
        {
            toast.Close();
            Snooze(alert, minutes);
        };
        toast.Closed += (_, _) =>
        {
            _open.Remove(toast);
            Arrange();
            if (_waiting.Count > 0) Show(_waiting.Dequeue());
        };

        _open.Add(toast);
        toast.Show();
        Arrange();

        if (_settings.Current.PlayReminderSound) SystemSounds.Asterisk.Play();
    }

    private void Snooze(ReminderAlert alert, int minutes)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(minutes) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Show(alert with { FireAt = DateTime.Now });
        };
        timer.Start();
    }

    /// <summary>Newest at the bottom, older ones stacked above it.</summary>
    private void Arrange()
    {
        var area = SystemParameters.WorkArea;
        var bottom = area.Bottom - Gap;
        for (var i = _open.Count - 1; i >= 0; i--)
        {
            var toast = _open[i];
            toast.UpdateLayout();
            var height = toast.ActualHeight > 0 ? toast.ActualHeight : 130;
            toast.Left = area.Right - toast.Width - Gap;
            toast.Top = bottom - height;
            bottom -= height + Gap;
        }
    }
}
