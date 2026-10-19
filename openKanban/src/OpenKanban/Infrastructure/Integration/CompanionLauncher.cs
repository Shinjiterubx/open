using System.Diagnostics;
using OpenKanban.Core.Abstractions;

namespace OpenKanban.Infrastructure.Integration;

/// <summary>
/// Opens notes in openNotes (<c>--open-note {id}</c>) and days in openCalendar (<c>--goto yyyy-MM-dd</c>).
/// Both apps are single-instance, so a running instance receives the request.
/// </summary>
public sealed class CompanionLauncher : ICompanionLauncher
{
    public bool IsOpenNotesAvailable => AppRegistry.FindExecutable("openNotes") is not null;
    public bool IsOpenCalendarAvailable => AppRegistry.FindExecutable("openCalendar") is not null;

    public bool TryOpenNote(Guid noteId) => TryStart("openNotes", "--open-note", noteId.ToString("N"));

    public bool TryShowInCalendar(DateOnly date) => TryStart("openCalendar", "--goto", date.ToString("yyyy-MM-dd"));

    private static bool TryStart(string app, params string[] arguments)
    {
        var exe = AppRegistry.FindExecutable(app);
        if (exe is null) return false;

        try
        {
            var startInfo = new ProcessStartInfo(exe) { UseShellExecute = false };
            foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
