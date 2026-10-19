using Microsoft.Win32;

namespace OpenCalendar.Infrastructure.Integration;

/// <summary>
/// Finds the openNotes installation and data folder.
/// openNotes publishes both under <c>HKCU\Software\openNotes</c> (ExePath, NotesDirectory) on every start;
/// the default install / data locations are used as fallback.
/// </summary>
public static class OpenNotesLocator
{
    private const string RegistryKey = @"Software\openNotes";

    public static string? FindNotesFolder()
    {
        var fromRegistry = ReadRegistry("NotesDirectory");
        if (fromRegistry is not null && Directory.Exists(fromRegistry)) return fromRegistry;

        var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "openNotes", "Notes");
        return Directory.Exists(fallback) ? fallback : null;
    }

    public static string? FindExecutable()
    {
        var candidates = new[]
        {
            ReadRegistry("ExePath"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "openNotes", "openNotes.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "openNotes", "openNotes.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "openNotes", "openNotes.exe"),
        };
        return candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
    }

    private static string? ReadRegistry(string name)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey);
            return key?.GetValue(name) as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
