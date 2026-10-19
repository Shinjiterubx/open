using OpenCalendar.Core.Models;
using OpenCalendar.Core.Recurrence;
using Xunit;

namespace OpenCalendar.Tests;

public class RecurrenceTests
{
    private static CalendarEvent Event(DateTime start, RecurrenceRule? rule = null, int minutes = 60) => new()
    {
        Title = "teszt",
        Start = start,
        End = start.AddMinutes(minutes),
        Recurrence = rule,
    };

    private static List<DateTime> Starts(CalendarEvent ev, DateTime from, DateTime to) => OccurrenceExpander.Expand(ev, from, to).ToList();

    [Fact]
    public void Single_event_appears_once()
    {
        var ev = Event(new DateTime(2026, 10, 13, 9, 0, 0));
        Assert.Single(Starts(ev, new DateTime(2026, 10, 1), new DateTime(2026, 11, 1)));
        Assert.Empty(Starts(ev, new DateTime(2026, 11, 1), new DateTime(2026, 12, 1)));
    }

    [Fact]
    public void Daily_with_interval()
    {
        var ev = Event(new DateTime(2026, 10, 1, 8, 0, 0), new RecurrenceRule { Frequency = RecurrenceFrequency.Daily, Interval = 2 });
        var starts = Starts(ev, new DateTime(2026, 10, 1), new DateTime(2026, 10, 8));
        Assert.Equal(new[] { 1, 3, 5, 7 }, starts.Select(s => s.Day));
    }

    [Fact]
    public void Weekly_on_selected_days()
    {
        // 2026-10-13 is a Tuesday
        var rule = new RecurrenceRule
        {
            Frequency = RecurrenceFrequency.Weekly,
            DaysOfWeek = new List<DayOfWeek> { DayOfWeek.Tuesday, DayOfWeek.Thursday },
        };
        var ev = Event(new DateTime(2026, 10, 13, 10, 0, 0), rule);
        var starts = Starts(ev, new DateTime(2026, 10, 12), new DateTime(2026, 10, 26));
        Assert.Equal(new[] { 13, 15, 20, 22 }, starts.Select(s => s.Day));
        Assert.All(starts, s => Assert.Equal(10, s.Hour));
    }

    [Fact]
    public void Monthly_on_the_31st_skips_short_months()
    {
        var ev = Event(new DateTime(2026, 1, 31, 12, 0, 0), new RecurrenceRule { Frequency = RecurrenceFrequency.Monthly });
        var starts = Starts(ev, new DateTime(2026, 1, 1), new DateTime(2026, 6, 1));
        Assert.Equal(new[] { 1, 3, 5 }, starts.Select(s => s.Month));
    }

    [Fact]
    public void Count_and_until_limit_the_series()
    {
        var byCount = Event(new DateTime(2026, 10, 1, 8, 0, 0),
            new RecurrenceRule { Frequency = RecurrenceFrequency.Daily, EndKind = RecurrenceEnd.AfterCount, Count = 3 });
        Assert.Equal(3, Starts(byCount, new DateTime(2026, 9, 1), new DateTime(2026, 12, 1)).Count);

        var byDate = Event(new DateTime(2026, 10, 1, 8, 0, 0),
            new RecurrenceRule { Frequency = RecurrenceFrequency.Daily, EndKind = RecurrenceEnd.OnDate, Until = new DateOnly(2026, 10, 5) });
        Assert.Equal(5, Starts(byDate, new DateTime(2026, 9, 1), new DateTime(2026, 12, 1)).Count);
    }

    [Fact]
    public void Exception_dates_are_skipped()
    {
        var ev = Event(new DateTime(2026, 10, 1, 8, 0, 0), new RecurrenceRule { Frequency = RecurrenceFrequency.Daily });
        ev.ExceptionDates.Add(new DateOnly(2026, 10, 2));
        var starts = Starts(ev, new DateTime(2026, 10, 1), new DateTime(2026, 10, 4));
        Assert.Equal(new[] { 1, 3 }, starts.Select(s => s.Day));
    }

    [Fact]
    public void Long_running_daily_series_is_fast_and_correct_far_in_the_future()
    {
        var ev = Event(new DateTime(2000, 1, 1, 7, 0, 0), new RecurrenceRule { Frequency = RecurrenceFrequency.Daily });
        var starts = Starts(ev, new DateTime(2026, 10, 13), new DateTime(2026, 10, 14));
        Assert.Equal(new DateTime(2026, 10, 13, 7, 0, 0), Assert.Single(starts));
    }

    [Fact]
    public void Event_overlapping_range_start_is_included()
    {
        var ev = Event(new DateTime(2026, 10, 12, 23, 0, 0), minutes: 120);
        Assert.Single(Starts(ev, new DateTime(2026, 10, 13), new DateTime(2026, 10, 14)));
    }
}
