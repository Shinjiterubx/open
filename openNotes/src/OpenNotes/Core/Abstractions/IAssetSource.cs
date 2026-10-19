namespace OpenNotes.Core.Abstractions;

/// <summary>Reads embedded note assets by their Markdown path (e.g. <c>assets/photo.png</c>).</summary>
public interface IAssetSource
{
    /// <returns>The file content, or <c>null</c> if the note has no such asset.</returns>
    byte[]? GetAsset(string relativePath);
}
