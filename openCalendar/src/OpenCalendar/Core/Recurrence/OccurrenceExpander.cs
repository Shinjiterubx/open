using OpenCalendar.Core.Models;
using OpenCalendar.Core.Time;

namespace OpenCalendar.Core.Recurrence;

/// <summary>Turns events (single or recurring) into concrete occurrences within a time range.</summary>
public static class OccurrenceExpander
{
    /// <summary>Upper bound of generated candidates per event and query (protects against bad data).</summary>
    private const int SafetyLimit = 20000;

    /// <summary>Occurrences of all events of the given calendars that overlap [rangeStart, rangeEnd), sorted by start.</summary>
    public static IReadOnlyList<Occurrence> Expand(IEnumerable<CalendarDocument> calendars, DateTime rangeStart, DateTime rangeEnd) =>
        calendars
            .SelectMany(c => c.Events.SelectMany(e => Expand(e, rangeStart, rangeEnd)
                .Select(start => new Occurrence(e, c, start, start + e.Duration))))
            .OrderBy(o => o.Start)
            .ThenByDescending(o => o.IsAllDay)
            .ToList();

    /// <summary>Start times of the occurrences of <paramref name="ev"/> that overlap [rangeStart, rangeEnd).</summary>
    public static IEnumerable<DateTime> Expand(CalendarEvent ev, DateTime rangeStart, DateTime rangeEnd)
    {
        var duration = ev.Duration;
        var exceptions = ev.ExceptionDates.Count == 0 ? null : new HashSet<DateOnly>(ev.ExceptionDates);

        foreach (var start in Candidates(ev, rangeStart - duration))
        {
            if (start >= rangeEnd) yield break;
            if (start + duration <= rangeStart) continue;
            if (exceptions is not null && exceptions.Contains(DateOnly.FromDateTime(start))) continue;
            yield return start;
        }
    }

    /// <summary>All occurrence starts in chronological order, honouring interval, weekdays, end date and count.</summary>
    private static IEnumerable<DateTime> Candidates(CalendarEvent ev, DateTime notBefore)
    {
        var first = ev.IsAllDay ? ev.Start.Date : ev.Start;
        var rule = ev.Recurrence;

        if (rule is null || rule.Frequency == RecurrenceFrequency.None)
        {
            yield return first;
            yield break;
        }

        var interval = Math.Max(1, rule.Interval);
        var until = rule.EndKind == RecurrenceEnd.OnDate ? rule.Until : null;
        var maxCount = rule.EndKind == RecurrenceEnd.AfterCount ? Math.Max(1, rule.Count ?? 1) : int.MaxValue;
        var produced = 0;

        foreach (var candidate in Sequence(first, rule, interval, maxCount == int.MaxValue ? notBefore : first))
        {
            if (until is { } last && DateOnly.FromDateTime(candidate) > last) yield break;
            if (produced++ >= maxCount) yield break;
            if (produced > SafetyLimit) yield break;
            yield return candidate;
        }
    }

    /// <param name="skipTo">Optimisation hint: candidates well before this may be skipped (only valid without a count limit).</param>
    private static IEnumerable<DateTime> Sequence(DateTime first, RecurrenceRule rule, int interval, DateTime skipTo)
    {
        switch (rule.Frequency)
        {
            case RecurrenceFrequency.Daily:
            {
                var k = 0L;
                if (skipTo > first) k = (long)((skipTo - first).TotalDays / interval) - 1;
                for (k = Math.Max(0, k); ; k++)
                    yield return first.AddDays(k * interval);
            }

            case RecurrenceFrequency.Weekly:
            {
                var days = rule.DaysOfWeek.Count > 0 ? rule.DaysOfWeek.Distinct().ToList() : new List<DayOfWeek> { first.DayOfWeek };
                var offsets = days.Select(WeekMath.MondayBasedIndex).OrderBy(i => i).ToList();
                var weekStart = WeekMath.StartOfWeek(first.Date);
                var w = 0L;
                if (skipTo > first) w = Math.Max(0, (long)((skipTo - weekStart).TotalDays / 7 / interval) - 1);
                for (; ; w++)
                {
                    var week = weekStart.AddDays(w * 7 * interval);
                    foreach (var offset in offsets)
                    {
                        var candidate = week.AddDays(offset) + first.TimeOfDay;
                        if (candidate >= first) yield return candidate;
                    }
                }
            }

            case RecurrenceFrequency.Monthly:
                for (var k = 0; ; k++)
                {
                    var month = new DateTime(first.Year, first.Month, 1).AddMonths(k * interval);
                    if (first.Day > DateTime.DaysInMonth(month.Year, month.Month)) continue;   // e.g. the 31st: skip short months
                    yield return new DateTime(month.Year, month.Month, first.Day) + first.TimeOfDay;
                }

            case RecurrenceFrequency.Yearly:
                for (var k = 0; ; k++)
                {
                    var year = first.Year + k * interval;
                    if (year > 9998) yield break;
                    if (first.Month == 2 && first.Day == 29 && !DateTime.IsLeapYear(year)) continue;
                    yield return new DateTime(year, first.Month, first.Day) + first.TimeOfDay;
                }

            default:
                yield return first;
                break;
        }
    }
}
