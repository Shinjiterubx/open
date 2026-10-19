using Microsoft.Win32;
using OpenNotes.Core.Abstractions;

namespace OpenNotes.Infrastructure.Platform;

/// <summary>
/// Publishes where openNotes and its notes live under <c>HKCU\Software\openNotes</c>,
/// so companion apps (openCalendar) can read deadlines and open notes – also for portable installs.
/// </summary>
public static class AppRegistration
{
    private const string KeyPath = @"Software\openNotes";

    public static void Publish(IAppPaths paths)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
            if (Environment.ProcessPath is { } exe) key.SetValue("ExePath", exe);
            key.SetValue("NotesDirectory", paths.NotesDirectory);
            key.SetValue("Version", AppInfo.Version);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Integration is optional – never block start-up.
        }
    }
}
