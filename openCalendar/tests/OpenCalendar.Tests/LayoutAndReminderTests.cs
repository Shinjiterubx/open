using OpenCalendar.Core.Agenda;
using OpenCalendar.Core.Models;
using OpenCalendar.Core.Reminders;
using OpenCalendar.Infrastructure.Storage;
using Xunit;

namespace OpenCalendar.Tests;

public class LayoutAndReminderTests
{
    [Fact]
    public void Overlapping_items_get_side_by_side_columns()
    {
        var slots = DayLayout.Arrange(new List<(double, double)> { (540, 600), (570, 630), (700, 760) });
        Assert.Equal(new LayoutSlot(0, 2), slots[0]);
        Assert.Equal(new LayoutSlot(1, 2), slots[1]);
        Assert.Equal(new LayoutSlot(0, 1), slots[2]);
    }

    [Fact]
    public void Recurring_reminder_fires_for_each_occurrence()
    {
        var calendar = new CalendarDocument();
        calendar.Events.Add(new CalendarEvent
        {
            Title = "Napi meeting",
            Start = new DateTime(2026, 10, 1, 9, 0, 0),
            End = new DateTime(2026, 10, 1, 9, 15, 0),
            Recurrence = new RecurrenceRule { Frequency = RecurrenceFrequency.Daily },
            ReminderMinutes = new List<int> { 10 },
        });

        var alerts = ReminderPlanner.Due(new[] { calendar }, Array.Empty<NoteReference>(), -1,
            new DateTime(2026, 10, 13, 8, 45, 0), new DateTime(2026, 10, 13, 8, 55, 0));

        var alert = Assert.Single(alerts);
        Assert.Equal(new DateTime(2026, 10, 13, 8, 50, 0), alert.FireAt);
        Assert.Equal(new DateTime(2026, 10, 13, 9, 0, 0), alert.OccurrenceStart);
    }

    [Fact]
    public void Note_deadline_with_time_reminds()
    {
        var note = new NoteReference(Guid.NewGuid(), "Leadás", new DateOnly(2026, 10, 13), new TimeOnly(14, 0), false, true,
            NotePriority.High, DateTimeOffset.Now);

        var alerts = ReminderPlanner.Due(Array.Empty<CalendarDocument>(), new[] { note }, 15,
            new DateTime(2026, 10, 13, 13, 40, 0), new DateTime(2026, 10, 13, 13, 50, 0));

        Assert.Equal(note.Id, Assert.Single(alerts).NoteId);
    }

    [Fact]
    public void Notes_become_calendar_entries()
    {
        var timed = new NoteReference(Guid.NewGuid(), "A", new DateOnly(2026, 10, 13), new TimeOnly(10, 0), false, true, NotePriority.None, DateTimeOffset.Now);
        var done = timed with { Id = Guid.NewGuid(), IsDone = true };

        var entries = AgendaBuilder.Build(Array.Empty<CalendarDocument>(), new[] { timed, done }, includeDoneNotes: false,
            new DateTime(2026, 10, 13), new DateTime(2026, 10, 14));

        var entry = Assert.Single(entries);
        Assert.Equal(EntryKind.NoteDeadline, entry.Kind);
        Assert.Equal(new DateTime(2026, 10, 13, 10, 0, 0), entry.Start);
    }

    [Fact]
    public void Kanban_cards_become_calendar_entries()
    {
        var card = new KanbanCardReference(Guid.NewGuid(), "Leadás", new DateOnly(2026, 10, 14), new TimeOnly(15, 0), false,
            "Munka", "Sprint", "Folyamatban", "#2F5A8A");
        var done = card with { CardId = Guid.NewGuid(), IsDone = true };

        var entries = AgendaBuilder.Build(Array.Empty<CalendarDocument>(), Array.Empty<NoteReference>(), includeDoneNotes: false,
            new DateTime(2026, 10, 14), new DateTime(2026, 10, 15), new[] { card, done });

        var entry = Assert.Single(entries);
        Assert.Equal(EntryKind.KanbanCard, entry.Kind);
        Assert.Equal(new DateTime(2026, 10, 14, 15, 0, 0), entry.Start);
        Assert.Equal("Munka › Sprint › Folyamatban", entry.Location);
    }

    [Fact]
    public void Calendar_round_trips_through_opencalendar_file()
    {
        var calendar = new CalendarDocument { Name = "Munka", Color = "#3E8FC9" };
        calendar.Events.Add(new CalendarEvent
        {
            Title = "Sprint review",
            Start = new DateTime(2026, 10, 16, 14, 0, 0),
            End = new DateTime(2026, 10, 16, 15, 0, 0),
            Recurrence = new RecurrenceRule { Frequency = RecurrenceFrequency.Weekly, Interval = 2, DaysOfWeek = new List<DayOfWeek> { DayOfWeek.Friday } },
            ReminderMinutes = new List<int> { 15, 1440 },
            Note = new LinkedNote(Guid.NewGuid(), "Review jegyzet"),
        });

        var serializer = new OpenCalendarSerializer();
        using var stream = new MemoryStream();
        serializer.Write(new[] { calendar }, stream);
        stream.Position = 0;

        var copy = Assert.Single(serializer.Read(stream));
        var ev = Assert.Single(copy.Events);
        Assert.Equal(calendar.Id, copy.Id);
        Assert.Equal("Munka", copy.Name);
        Assert.Equal(new DateTime(2026, 10, 16, 14, 0, 0), ev.Start);
        Assert.Equal(RecurrenceFrequency.Weekly, ev.Recurrence!.Frequency);
        Assert.Equal(2, ev.Recurrence.Interval);
        Assert.Equal(new[] { 15, 1440 }, ev.ReminderMinutes);
        Assert.Equal("Review jegyzet", ev.Note!.Title);
    }
}
