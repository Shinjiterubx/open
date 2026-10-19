namespace OpenNotes.Core.Abstractions;

/// <summary>Controls whether the app starts together with Windows.</summary>
public interface IAutoStartService
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}
