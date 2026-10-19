using System.Text.Json.Serialization;

namespace OpenKanban.Core.Models;

/// <summary>Top level container (e.g. "Munka", "Otthon"): a set of boards. One workspace = one .openKanban file.</summary>
public sealed class Workspace
{
    public static readonly string[] Palette =
    {
        "#2F5A8A", "#3E8FC9", "#2E8B57", "#E0B23B", "#E58A3C", "#D45D79", "#6E63D8", "#6B7280",
    };

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Workspace";
    public string? Description { get; set; }
    public string Color { get; set; } = Palette[0];
    public List<Board> Boards { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    [JsonIgnore]
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "W" : Name.Trim()[..1].ToUpperInvariant();
}
