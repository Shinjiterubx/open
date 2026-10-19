using OpenNotes.Core.Models;
using OpenNotes.Core.Reminders;
using OpenNotes.Infrastructure.Storage;
using Xunit;

namespace OpenNotes.Tests;

public class ReminderTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0);

    private static Note Task(string title, int dayOffset, TimeOnly? time = null, NotePriority priority = NotePriority.None) => new()
    {
        Title = title,
        DueDate = DateOnly.FromDateTime(Now).AddDays(dayOffset),
        DueTime = time,
        ReminderEnabled = true,
        Priority = priority,
    };

    [Fact]
    public void Classifies_overdue_today_and_upcoming()
    {
        var entries = ReminderRules.Collect(new[]
        {
            Task("upcoming", 3),
            Task("today", 0),
            Task("overdue", -2),
            Task("today-earlier", 0, new TimeOnly(9, 0)),   // 09:00 already passed at 12:00 → overdue
        }, Now);

        Assert.Equal(new[] { "overdue", "today-earlier", "today", "upcoming" }.OrderBy(x => x),
            entries.Select(e => e.Note.Title).OrderBy(x => x));
        Assert.Equal(ReminderState.Overdue, entries.Single(e => e.Note.Title == "today-earlier").State);
        Assert.Equal(ReminderState.Today, entries.Single(e => e.Note.Title == "today").State);
        Assert.Equal(new ReminderCounts(2, 1, 1), ReminderRules.Count(entries));
    }

    [Fact]
    public void Done_or_disabled_reminders_are_not_active()
    {
        var done = Task("done", 0);
        done.IsDone = true;
        var off = Task("off", 0);
        off.ReminderEnabled = false;

        Assert.Empty(ReminderRules.Collect(new[] { done, off }, Now));
    }

    [Fact]
    public void Higher_priority_comes_first_within_a_group()
    {
        var entries = ReminderRules.Collect(new[]
        {
            Task("low", 5, priority: NotePriority.Low),
            Task("high", 6, priority: NotePriority.High),
        }, Now);

        Assert.Equal("high", entries[0].Note.Title);
    }

    [Theory]
    [InlineData("9", 9, 0)]
    [InlineData("930", 9, 30)]
    [InlineData("14:05", 14, 5)]
    [InlineData("08.15", 8, 15)]
    public void Time_input_is_lenient(string input, int hour, int minute)
    {
        Assert.True(TimeInput.TryParse(input, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
    }

    [Fact]
    public void Invalid_time_is_rejected_and_empty_means_none()
    {
        Assert.False(TimeInput.TryParse("25:99", out _));
        Assert.True(TimeInput.TryParse("", out var none));
        Assert.Null(none);
    }

    [Fact]
    public void Task_fields_survive_openNote_round_trip()
    {
        var note = Task("bevásárlás", 2, new TimeOnly(17, 30), NotePriority.High);
        var serializer = new OpenNoteSerializer();

        using var stream = new MemoryStream();
        serializer.Write(note, stream);
        stream.Position = 0;
        var copy = serializer.Read(stream);

        Assert.Equal(NotePriority.High, copy.Priority);
        Assert.Equal(note.DueDate, copy.DueDate);
        Assert.Equal(new TimeOnly(17, 30), copy.DueTime);
        Assert.True(copy.ReminderEnabled);
        Assert.False(copy.IsDone);
    }
}
