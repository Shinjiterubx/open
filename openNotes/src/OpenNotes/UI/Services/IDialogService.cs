namespace OpenNotes.UI.Services;

public enum DialogChoice
{
    Primary,
    Secondary,
    Cancel,
}

/// <summary>User interaction the view models need, without knowing about windows.</summary>
public interface IDialogService
{
    IReadOnlyList<string> PickFilesToOpen(string title, string filter);

    string? PickSaveFile(string title, string filter, string defaultFileName);

    bool Confirm(string title, string message, string confirmText, bool destructive = false);

    DialogChoice Ask(string title, string message, string primaryText, string secondaryText);

    void ShowError(string title, string message);

    /// <summary>Name + colour of a folder; <c>null</c> if cancelled.</summary>
    FolderDialogResult? EditFolder(string title, string name, string color, IReadOnlyList<string> palette, string confirmText);
}

public sealed record FolderDialogResult(string Name, string Color);
