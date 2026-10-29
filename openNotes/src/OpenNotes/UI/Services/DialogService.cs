using System.Windows;
using Microsoft.Win32;
using OpenNotes.UI.Views;

namespace OpenNotes.UI.Services;

public sealed class DialogService : IDialogService
{
    public IReadOnlyList<string> PickFilesToOpen(string title, string filter)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = filter, Multiselect = true };
        return dialog.ShowDialog(ActiveWindow()) == true ? dialog.FileNames : Array.Empty<string>();
    }

    public string? PickSaveFile(string title, string filter, string defaultFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = filter,
            FileName = defaultFileName,
            AddExtension = true,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog(ActiveWindow()) == true ? dialog.FileName : null;
    }

    public bool Confirm(string title, string message, string confirmText, bool destructive = false) =>
        ThemedDialog.Present(ActiveWindow(), title, message, confirmText, destructive: destructive) == DialogChoice.Primary;

    public DialogChoice Ask(string title, string message, string primaryText, string secondaryText) =>
        ThemedDialog.Present(ActiveWindow(), title, message, primaryText, secondaryText);

    public void ShowError(string title, string message) =>
        ThemedDialog.Present(ActiveWindow(), title, message, "Rendben", cancelText: null, isError: true);

    public FolderDialogResult? EditFolder(string title, string name, string color, IReadOnlyList<string> palette, string confirmText) =>
        FolderDialog.Present(ActiveWindow(), title, name, color, palette, confirmText);

    private static Window? ActiveWindow()
    {
        var windows = Application.Current.Windows.OfType<Window>().Where(w => w.IsVisible && w is not ThemedDialog && w is not FolderDialog).ToList();
        return windows.FirstOrDefault(w => w.IsActive) ?? windows.FirstOrDefault();
    }
}
