using System.Globalization;

namespace OpenCalendar.Core.Time;

/// <summary>Lenient time-of-day input: "9", "930", "9:30", "09.30", "14:05". Empty = no time.</summary>
public static class TimeInput
{
    public static bool TryParse(string? text, out TimeOnly? time)
    {
        time = null;
        var value = (text ?? string.Empty).Trim().Replace('.', ':');
        if (value.Length == 0) return true;

        if (!value.Contains(':'))
        {
            // "9" → 09:00, "930" → 09:30, "1430" → 14:30
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var digits)) return false;
            value = value.Length <= 2 ? $"{digits}:00" : $"{digits / 100}:{digits % 100:00}";
        }

        if (!TimeOnly.TryParseExact(value, new[] { "H:mm", "HH:mm", "H:m" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return false;

        time = parsed;
        return true;
    }
}
