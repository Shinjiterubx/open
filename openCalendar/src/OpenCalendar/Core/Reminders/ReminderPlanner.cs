using OpenCalendar.Core.Models;
using OpenCalendar.Core.Recurrence;

namespace OpenCalendar.Core.Reminders;

/// <summary>A reminder that should be shown to the user.</summary>
public sealed record ReminderAlert(
    string Key,
    string Title,
    DateTime OccurrenceStart,
    DateTime FireAt,
    int MinutesBefore,
    bool IsAllDay,
    string? Location,
    Guid? EventId,
    Guid? NoteId);

/// <summary>Works out which reminders fall into a time window (each occurrence of a recurring event reminds).</summary>
public static class ReminderPlanner
{
    /// <summary>All-day items remind relative to 09:00 on their day.</summary>
    public static readonly TimeSpan AllDayAnchor = TimeSpan.FromHours(9);

    public static DateTime AnchorOf(DateTime start, bool isAllDay) => isAllDay ? start.Date + AllDayAnchor : start;

    /// <summary>Reminders with a fire time in (<paramref name="from"/>, <paramref name="to"/>].</summary>
    public static IReadOnlyList<ReminderAlert> Due(
        IEnumerable<CalendarDocument> calendars,
        IEnumerable<NoteReference> notes,
        int noteReminderMinutes,
        DateTime from,
        DateTime to)
    {
        var alerts = new List<ReminderAlert>();
        var calendarList = calendars.ToList();

        var maxOffset = calendarList.SelectMany(c => c.Events).SelectMany(e => e.ReminderMinutes).DefaultIfEmpty(0).Max();
        var occurrences = OccurrenceExpander.Expand(calendarList, from.AddDays(-1), to.AddMinutes(maxOffset).AddDays(1));

        foreach (var occurrence in occurrences)
        {
            var anchor = AnchorOf(occurrence.Start, occurrence.IsAllDay);
            foreach (var minutes in occurrence.Event.ReminderMinutes.Distinct())
            {
                var fireAt = anchor.AddMinutes(-minutes);
                if (fireAt <= from || fireAt > to) continue;

                alerts.Add(new ReminderAlert($"{occurrence.Key}|{minutes}", occurrence.Event.DisplayTitle, occurrence.Start,
                    fireAt, minutes, occurrence.IsAllDay, occurrence.Event.Location, occurrence.Event.Id, occurrence.Event.Note?.NoteId));
            }
        }

        if (noteReminderMinutes >= 0)
        {
            foreach (var note in notes)
            {
                if (note.IsDone || !note.ReminderEnabled || note.DueDate is not { } date || note.DueTime is not { } time) continue;

                var start = date.ToDateTime(time);
                var fireAt = start.AddMinutes(-noteReminderMinutes);
                if (fireAt <= from || fireAt > to) continue;

                alerts.Add(new ReminderAlert($"note:{note.Id:N}@{start:yyyyMMddHHmm}", note.DisplayTitle, start, fireAt,
                    noteReminderMinutes, false, null, null, note.Id));
            }
        }

        return alerts.OrderBy(a => a.FireAt).ToList();
    }

    /// <summary>"Most kezdődik", "15 perc múlva", "1 óra múlva", "holnap"...</summary>
    public static string DescribeOffset(int minutesBefore) => minutesBefore switch
    {
        <= 0 => "Most kezdődik",
        < 60 => $"{minutesBefore} perc múlva",
        < 1440 when minutesBefore % 60 == 0 => $"{minutesBefore / 60} óra múlva",
        < 1440 => $"{minutesBefore / 60} óra {minutesBefore % 60} perc múlva",
        _ when minutesBefore % 1440 == 0 => minutesBefore == 1440 ? "Holnap" : $"{minutesBefore / 1440} nap múlva",
        _ => $"{minutesBefore / 60} óra múlva",
    };

    /// <summary>Label for a reminder offset in pickers: "Kezdéskor", "15 perccel előtte", "1 nappal előtte".</summary>
    public static string LabelOf(int minutesBefore) => minutesBefore switch
    {
        <= 0 => "Kezdéskor",
        < 60 => $"{minutesBefore} perccel előtte",
        < 1440 when minutesBefore % 60 == 0 => $"{minutesBefore / 60} órával előtte",
        < 10080 when minutesBefore % 1440 == 0 => $"{minutesBefore / 1440} nappal előtte",
        _ when minutesBefore % 10080 == 0 => $"{minutesBefore / 10080} héttel előtte",
        _ => $"{minutesBefore} perccel előtte",
    };

    public static readonly int[] Presets = { 0, 5, 10, 15, 30, 60, 120, 1440, 2880, 10080 };
}
