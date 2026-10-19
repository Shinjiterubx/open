using OpenKanban.Core.Models;

namespace OpenKanban.Core.Boards;

public enum DueState
{
    None,
    Done,
    Overdue,
    DueSoon,
    Upcoming,
}

/// <summary>Deadline classification and Hungarian texts for cards.</summary>
public static class CardDue
{
    /// <summary>"Due soon" window.</summary>
    public static readonly TimeSpan SoonWindow = TimeSpan.FromHours(24);

    public static DueState Classify(Card card, DateTime now)
    {
        if (card.DueMoment is not { } due) return DueState.None;
        if (card.IsDone) return DueState.Done;
        if (due < now) return DueState.Overdue;
        return due - now <= SoonWindow ? DueState.DueSoon : DueState.Upcoming;
    }

    public static string ShortText(Card card, DateTime now, System.Globalization.CultureInfo culture)
    {
        if (card.DueDate is not { } date) return string.Empty;
        var day = date.DayNumber - DateOnly.FromDateTime(now).DayNumber;
        var text = day switch
        {
            0 => "Ma",
            1 => "Holnap",
            -1 => "Tegnap",
            _ => date.ToString(date.Year == now.Year ? "MMM d." : "yyyy. MMM d.", culture),
        };
        return card.DueTime is { } time ? $"{text} {time.ToString("HH:mm", culture)}" : text;
    }
}
