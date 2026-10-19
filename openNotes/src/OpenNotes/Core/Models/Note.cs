namespace OpenNotes.Core.Models;

/// <summary>
/// A single note: Markdown content plus the binary assets (images) it embeds.
/// Assets are referenced from the Markdown with relative paths like <c>assets/photo.png</c>.
/// </summary>
public sealed class Note
{
    public const string AssetFolder = "assets";
    public const string UntitledTitle = "Névtelen jegyzet";

    private readonly Dictionary<string, byte[]> _assets = new(StringComparer.OrdinalIgnoreCase);

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public bool IsPinned { get; set; }

    public NotePriority Priority { get; set; }

    /// <summary>Day the note is due (local date). <c>null</c> = no deadline.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Optional time of day; without it the note is due at the end of <see cref="DueDate"/>.</summary>
    public TimeOnly? DueTime { get; set; }

    /// <summary>Remind daily until <see cref="IsDone"/> is set or the reminder is turned off.</summary>
    public bool ReminderEnabled { get; set; }

    public bool IsDone { get; set; }

    public IReadOnlyDictionary<string, byte[]> Assets => _assets;

    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? UntitledTitle : Title.Trim();

    /// <summary>
    /// Adds an asset and returns the relative path to use in Markdown.
    /// Identical content is stored only once.
    /// </summary>
    public string AddAsset(string fileName, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        foreach (var (existingName, existingData) in _assets)
        {
            if (existingData.AsSpan().SequenceEqual(data))
                return AssetName.ToRelativePath(existingName);
        }

        var name = AssetName.MakeUnique(AssetName.Sanitize(fileName), _assets.ContainsKey);
        _assets[name] = data;
        return AssetName.ToRelativePath(name);
    }

    /// <summary>Stores an asset under an exact (sanitized) name. Used when deserializing.</summary>
    public void SetAsset(string name, byte[] data) => _assets[AssetName.Sanitize(name)] = data;
}
