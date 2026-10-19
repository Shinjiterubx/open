using System.Diagnostics;
using OpenCalendar.Core.Abstractions;

namespace OpenCalendar.Infrastructure.Integration;

/// <summary>
/// Opens a note in openNotes with <c>openNotes.exe --open-note {id}</c>.
/// If openNotes is already running, its single-instance pipe forwards the request to the open window.
/// </summary>
public sealed class OpenNotesLauncher : INoteLauncher
{
    public const string OpenNoteArgument = "--open-note";

    public bool IsAvailable => OpenNotesLocator.FindExecutable() is not null;

    public bool TryOpen(Guid noteId)
    {
        var exe = OpenNotesLocator.FindExecutable();
        if (exe is null) return false;

        try
        {
            var startInfo = new ProcessStartInfo(exe) { UseShellExecute = false };
            startInfo.ArgumentList.Add(OpenNoteArgument);
            startInfo.ArgumentList.Add(noteId.ToString("N"));
            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
