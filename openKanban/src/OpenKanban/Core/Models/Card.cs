using System.Text.Json.Serialization;

namespace OpenKanban.Core.Models;

/// <summary>A task card.</summary>
public sealed class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Optional cover colour (#RRGGBB) shown as a strip on top of the card.</summary>
    public string? CoverColor { get; set; }

    public List<Guid> LabelIds { get; set; } = new();
    public List<ChecklistItem> Checklist { get; set; } = new();

    public DateOnly? DueDate { get; set; }
    public TimeOnly? DueTime { get; set; }
    public bool IsDone { get; set; }

    /// <summary>Optional link to an openNotes note.</summary>
    public LinkedNote? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "(Névtelen kártya)" : Title.Trim();

    /// <summary>Without a time the card is due at the end of the day.</summary>
    [JsonIgnore]
    public DateTime? DueMoment => DueDate?.ToDateTime(DueTime ?? new TimeOnly(23, 59, 59));

    public Card Clone()
    {
        var copy = (Card)MemberwiseClone();
        copy.LabelIds = new List<Guid>(LabelIds);
        copy.Checklist = Checklist.Select(i => new ChecklistItem { Id = i.Id, Text = i.Text, IsDone = i.IsDone }).ToList();
        return copy;
    }
}

public sealed class ChecklistItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public bool IsDone { get; set; }
}

/// <summary>Reference to an openNotes note (title cached for display).</summary>
public sealed record LinkedNote(Guid NoteId, string Title);

/// <summary>A note read from the openNotes library (metadata only).</summary>
public sealed record NoteReference(Guid Id, string Title)
{
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "Névtelen jegyzet" : Title.Trim();
}
