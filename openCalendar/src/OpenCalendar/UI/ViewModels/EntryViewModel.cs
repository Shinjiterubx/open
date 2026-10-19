using OpenCalendar.Core.Agenda;
using OpenCalendar.Core.Models;

namespace OpenCalendar.UI.ViewModels;

/// <summary>An event occurrence or openNotes deadline as shown in a view (immutable except layout).</summary>
public sealed class EntryViewModel
{
    public EntryViewModel(CalendarEntry entry, DateOnly? day = null)
    {
        Entry = entry;

        if (day is { } d && !entry.IsAllDay)
        {
            // Clip multi-day timed events to this day's column.
            var dayStart = d.ToDateTime(TimeOnly.MinValue);
            StartMinute = Math.Max(0, (entry.Start - dayStart).TotalMinutes);
            EndMinute = Math.Min(24 * 60, (entry.End - dayStart).TotalMinutes);
            if (EndMinute - StartMinute < 20) EndMinute = Math.Min(24 * 60, StartMinute + 20);
        }
    }

    public CalendarEntry Entry { get; }

    public string Title => Entry.Title;
    public string Color => Entry.Color;
    public string? Location => Entry.Location;
    public bool HasLocation => !string.IsNullOrWhiteSpace(Entry.Location);
    public bool IsAllDay => Entry.IsAllDay;
    public bool IsNote => Entry.Kind == EntryKind.NoteDeadline;
    public bool IsKanban => Entry.Kind == EntryKind.KanbanCard;
    public bool IsDone => Entry.IsDone;
    public bool IsRecurring => Entry.IsRecurring;
    public bool HasLinkedNote => Entry.LinkedNoteId is not null;
    public bool HasPriority => Entry.Priority != NotePriority.None;

    public string PriorityColor => Entry.Priority switch
    {
        NotePriority.High => "#E0434F",
        NotePriority.Medium => "#F0B429",
        NotePriority.Low => "#8B6BD6",
        _ => "#00000000",
    };

    public string ShortTime => Entry.IsAllDay ? string.Empty : Entry.Start.ToString("HH:mm");

    public string TimeText
    {
        get
        {
            if (IsNote) return Entry.IsAllDay ? "openNotes határidő" : $"Határidő {Entry.Start:HH:mm}";
            if (IsKanban) return Entry.IsAllDay ? "openKanban határidő" : $"Kártya határidő {Entry.Start:HH:mm}";
            if (Entry.IsAllDay)
                return Entry.End.Date > Entry.Start.Date ? $"Egész nap · {Day(Entry.Start)} – {Day(Entry.End)}" : "Egész nap";
            return Entry.End.Date > Entry.Start.Date
                ? $"{Day(Entry.Start)} {Entry.Start:HH:mm} – {Day(Entry.End)} {Entry.End:HH:mm}"
                : $"{Entry.Start:HH:mm} – {Entry.End:HH:mm}";
        }
    }

    private static string Day(DateTime value) => value.ToString("MMM d.", Hu.Culture);

    public string ToolTipText
    {
        get
        {
            var lines = new List<string> { Title, TimeText };
            if (HasLocation) lines.Add((IsKanban ? "🗂 " : "📍 ") + Location);
            if (IsRecurring) lines.Add("↻ Ismétlődő esemény");
            if (HasLinkedNote && !IsNote) lines.Add("📝 Kapcsolt jegyzet");
            if (IsNote) lines.Add(IsDone ? "Kész" : "Kattints a megnyitáshoz az openNotes-ban");
            if (IsKanban) lines.Add(IsDone ? "Kész" : "Kattints a kártya megnyitásához az openKanban-ban");
            return string.Join("\n", lines);
        }
    }

    // Layout inside a day column (minutes from midnight; column assignment for overlaps).
    public double StartMinute { get; }
    public double EndMinute { get; }
    public int Column { get; set; }
    public int ColumnCount { get; set; } = 1;
}
