using System.Windows.Threading;
using OpenCalendar.Core.Abstractions;
using OpenCalendar.Core.Reminders;
using OpenCalendar.Services;

namespace OpenCalendar.UI.Services;

/// <summary>
/// Checks every few seconds which reminders are due (every occurrence of recurring events, and
/// openNotes deadlines with a time) and shows each one once as a notification.
/// </summary>
public sealed class ReminderService : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(15);

    /// <summary>On start, reminders missed within this window (e.g. app just launched at login) still show.</summary>
    private static readonly TimeSpan CatchUpWindow = TimeSpan.FromMinutes(10);

    private readonly CalendarService _calendars;
    private readonly ISettingsStore _settings;
    private readonly ToastManager _toasts;
    private readonly DispatcherTimer _timer;
    private readonly HashSet<string> _shown = new();
    private DateTime _lastCheck;

    public ReminderService(CalendarService calendars, ISettingsStore settings, ToastManager toasts)
    {
        _calendars = calendars;
        _settings = settings;
        _toasts = toasts;
        _timer = new DispatcherTimer { Interval = TickInterval };
        _timer.Tick += (_, _) => Check();
    }

    public void Start()
    {
        _lastCheck = DateTime.Now - CatchUpWindow;
        Check();
        _timer.Start();
    }

    public void Dispose() => _timer.Stop();

    private void Check()
    {
        var now = DateTime.Now;
        if (now < _lastCheck) _lastCheck = now - TickInterval;   // clock moved backwards

        var alerts = ReminderPlanner.Due(_calendars.Calendars, _calendars.Notes, _settings.Current.OpenNotesReminderMinutes, _lastCheck, now);
        foreach (var alert in alerts)
        {
            if (_shown.Add(alert.Key)) _toasts.Show(alert);
        }

        _lastCheck = now;
        if (_shown.Count > 5000) _shown.Clear();
    }
}
