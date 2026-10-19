using OpenNotes.Core.Models;

namespace OpenNotes.Core.Abstractions;

/// <summary>Makes note assets reachable for the HTML preview.</summary>
public interface IPreviewAssetCache
{
    void Reset();

    void Sync(Note note);

    string RewriteUrl(Guid noteId, string url);
}
