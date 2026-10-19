namespace OpenCalendar.Core.Time;

/// <summary>Week helpers. Weeks start on Monday (Hungarian / ISO convention).</summary>
public static class WeekMath
{
    public static readonly DayOfWeek[] OrderedDays =
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
    };

    /// <summary>Monday = 0 … Sunday = 6.</summary>
    public static int MondayBasedIndex(DayOfWeek day) => ((int)day + 6) % 7;

    public static DateTime StartOfWeek(DateTime date) => date.Date.AddDays(-MondayBasedIndex(date.DayOfWeek));

    public static DateOnly StartOfWeek(DateOnly date) => date.AddDays(-MondayBasedIndex(date.DayOfWeek));

    /// <summary>First visible day of a 6-week month grid.</summary>
    public static DateOnly MonthGridStart(int year, int month) => StartOfWeek(new DateOnly(year, month, 1));
}
