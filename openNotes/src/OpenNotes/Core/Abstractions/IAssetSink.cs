namespace OpenNotes.Core.Abstractions;

/// <summary>Receives binary files (pasted / dropped images) and returns the Markdown path to reference them.</summary>
public interface IAssetSink
{
    bool CanAddAssets { get; }

    string AddAsset(string fileName, byte[] data);
}
