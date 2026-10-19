namespace OpenCalendar.Core.Models;

public enum RecurrenceFrequency
{
    None,
    Daily,
    Weekly,
    Monthly,
    Yearly,
}

public enum RecurrenceEnd
{
    Never,
    OnDate,
    AfterCount,
}

/// <summary>"Every <see cref="Interval"/> days / weeks (on <see cref="DaysOfWeek"/>) / months / years", with an optional end.</summary>
public sealed class RecurrenceRule
{
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Weekly;

    /// <summary>Every N units (1 = every day / week / ...).</summary>
    public int Interval { get; set; } = 1;

    /// <summary>Weekly only. Empty = the weekday of the event's start.</summary>
    public List<DayOfWeek> DaysOfWeek { get; set; } = new();

    public RecurrenceEnd EndKind { get; set; } = RecurrenceEnd.Never;

    /// <summary>Last date an occurrence may start on (inclusive) when <see cref="EndKind"/> is <see cref="RecurrenceEnd.OnDate"/>.</summary>
    public DateOnly? Until { get; set; }

    /// <summary>Total number of occurrences when <see cref="EndKind"/> is <see cref="RecurrenceEnd.AfterCount"/>.</summary>
    public int? Count { get; set; }

    public RecurrenceRule Clone()
    {
        var copy = (RecurrenceRule)MemberwiseClone();
        copy.DaysOfWeek = new List<DayOfWeek>(DaysOfWeek);
        return copy;
    }
}
