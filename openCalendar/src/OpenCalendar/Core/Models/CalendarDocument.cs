namespace OpenCalendar.Core.Models;

/// <summary>A named, coloured calendar with its events (one .opencalendar entry).</summary>
public sealed class CalendarDocument
{
    public static readonly string[] Palette =
    {
        "#D45D79", "#E58A3C", "#E0B23B", "#4FA36B", "#3E8FC9", "#6E63D8", "#A5539C", "#6B7280",
    };

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Naptár";

    /// <summary>Hex colour (#RRGGBB) used for the calendar's events.</summary>
    public string Color { get; set; } = Palette[0];

    public bool IsVisible { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public List<CalendarEvent> Events { get; set; } = new();
}
