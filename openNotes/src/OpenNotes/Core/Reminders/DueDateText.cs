using System.Globalization;
using OpenNotes.Core.Models;

namespace OpenNotes.Core.Reminders;

/// <summary>Hungarian, human friendly due date texts ("Ma 14:00", "Lejárt 2 napja", "okt. 20. · 7 nap múlva").</summary>
public static class DueDateText
{
    private static readonly CultureInfo Hungarian = CultureInfo.GetCultureInfo("hu-HU");

    /// <summary>Absolute form for editors: "okt. 20. 14:00" / "2027. jan. 5.".</summary>
    public static string Absolute(DateOnly date, TimeOnly? time, DateTime now)
    {
        var format = date.Year == now.Year ? "MMM d." : "yyyy. MMM d.";
        var text = date.ToString(format, Hungarian);
        return time is { } t ? $"{text} {t.ToString("HH:mm", Hungarian)}" : text;
    }

    /// <summary>Relative form for lists and the summary.</summary>
    public static string Relative(Note note, DateTime now)
    {
        if (note.DueDate is not { } date) return string.Empty;
        if (note.IsDone) return "Kész";

        var timeSuffix = note.DueTime is { } t ? " " + t.ToString("HH:mm", Hungarian) : string.Empty;
        var due = ReminderRules.DueMoment(note)!.Value;
        var dayDiff = date.DayNumber - DateOnly.FromDateTime(now).DayNumber;

        if (due < now)
        {
            return dayDiff switch
            {
                0 => "Lejárt ma" + timeSuffix,
                -1 => "Lejárt tegnap",
                _ => $"Lejárt {-dayDiff} napja",
            };
        }

        return dayDiff switch
        {
            0 => "Ma" + timeSuffix,
            1 => "Holnap" + timeSuffix,
            < 7 => $"{Capitalize(date.ToString("dddd", Hungarian))}{timeSuffix} · {dayDiff} nap múlva",
            _ => $"{Absolute(date, note.DueTime, now)} · {dayDiff} nap múlva",
        };
    }

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], Hungarian) + text[1..];
}
