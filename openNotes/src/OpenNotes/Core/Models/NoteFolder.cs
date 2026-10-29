namespace OpenNotes.Core.Models;

/// <summary>A folder notes can be filed into. Notes reference it by <see cref="Id"/>, so renaming is free.</summary>
public sealed class NoteFolder
{
    /// <summary>Folder colours offered in the folder dialog (the first one is the default).</summary>
    public static readonly IReadOnlyList<string> Palette = new[]
    {
        "#5B3E96", "#2F5A8A", "#2E8B57", "#C77D0A", "#D45D79", "#C42B4B", "#0E7C86", "#6B6B76",
    };

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = Palette[0];
}
