using OpenNotes.Core.Models;

namespace OpenNotes.Core.Abstractions;

/// <summary>System-wide keyboard shortcuts that work even when the app is in the background.</summary>
public interface IGlobalHotkeyService : IDisposable
{
    /// <returns><c>false</c> if the combination is invalid or already taken by another application.</returns>
    bool TryRegister(HotkeyGesture gesture, Action handler);

    void UnregisterAll();
}
