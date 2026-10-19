using OpenNotes.Core.Models;

namespace OpenNotes.Core.Abstractions;

public interface ISettingsStore
{
    AppSettings Current { get; }

    void Save();

    /// <summary>Raised after <see cref="Save"/> so views can react to changed settings.</summary>
    event EventHandler? Saved;
}
