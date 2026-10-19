using System.Windows.Threading;
using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Reminders;
using OpenNotes.UI.ViewModels;

namespace OpenNotes.UI.Services;

/// <summary>
/// Drives reminders while the app runs (also in the background / tray):
/// <list type="bullet">
/// <item>once a day – at start-up and at the configured time – shows the "remaining tasks" summary;</item>
/// <item>notifies when a note with a due <i>time</i> becomes due;</item>
/// <item>keeps "today / overdue" states current as the clock moves (incl. midnight).</item>
/// </list>
/// </summary>
public sealed class ReminderScheduler : IDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    private readonly MainViewModel _main;
    private readonly ISettingsStore _settings;
    private readonly IWindowService _windows;
    private readonly TrayIcon _tray;
    private readonly DispatcherTimer _timer;
    private DateTime _lastTick = DateTime.Now;

    public ReminderScheduler(MainViewModel main, ISettingsStore settings, IWindowService windows, TrayIcon tray)
    {
        _main = main;
        _settings = settings;
        _windows = windows;
        _tray = tray;
        _timer = new DispatcherTimer { Interval = TickInterval };
        _timer.Tick += (_, _) => Tick();
    }

    /// <summary>Starts the clock; shows today's summary right away if it hasn't been shown yet (first start of the day).</summary>
    public void Start()
    {
        _main.RefreshReminders();
        _lastTick = DateTime.Now;
        _timer.Start();

        if (ShouldShowDailySummary(DateTime.Now, requireTime: false))
            ShowDailySummary(notify: false);
    }

    public void Dispose() => _timer.Stop();

    private void Tick()
    {
        var now = DateTime.Now;
        _main.RefreshReminders();
        NotifyNewlyDue(_lastTick, now);

        if (ShouldShowDailySummary(now, requireTime: true))
            ShowDailySummary(notify: true);

        _lastTick = now;
    }

    private bool ShouldShowDailySummary(DateTime now, bool requireTime)
    {
        var settings = _settings.Current;
        if (!settings.DailySummaryEnabled || !_main.HasReminders) return false;
        if (settings.LastDailySummaryDate == DateOnly.FromDateTime(now)) return false;
        return !requireTime || TimeOnly.FromDateTime(now) >= settings.DailySummaryTime;
    }

    private void ShowDailySummary(bool notify)
    {
        _settings.Current.LastDailySummaryDate = DateOnly.FromDateTime(DateTime.Now);
        TrySaveSettings();

        _windows.ShowReminderSummary();
        if (notify)
            _tray.Notify("openNotes – mai teendők", _main.ReminderCountsText, isWarning: _main.HasOverdue, onClick: _windows.ShowReminderSummary);
    }

    /// <summary>Notes with an explicit due time that passed between two ticks.</summary>
    private void NotifyNewlyDue(DateTime from, DateTime to)
    {
        var due = _main.Reminders
            .Where(r => r.Note.Model.DueTime is not null)
            .Select(r => (r.Title, Due: ReminderRules.DueMoment(r.Note.Model)!.Value))
            .Where(r => r.Due > from && r.Due <= to)
            .ToList();
        if (due.Count == 0) return;

        var text = due.Count == 1 ? due[0].Title : string.Join(", ", due.Select(d => d.Title));
        _tray.Notify("Esedékes teendő", text, isWarning: true, onClick: _windows.ShowReminderSummary);
    }

    private void TrySaveSettings()
    {
        try { _settings.Save(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* saved again on exit */ }
    }
}
