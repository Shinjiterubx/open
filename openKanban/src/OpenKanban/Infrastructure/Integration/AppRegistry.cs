using Microsoft.Win32;

namespace OpenKanban.Infrastructure.Integration;

/// <summary>
/// Shared convention of the "open" apps: each publishes its location under <c>HKCU\Software\{app}</c>
/// (ExePath, data paths), so the others can find it – also for portable installs.
/// </summary>
public static class AppRegistry
{
    public static void Publish(string appName, IReadOnlyDictionary<string, string> values)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\{appName}", writable: true);
            foreach (var (name, value) in values) key.SetValue(name, value);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Integration is optional.
        }
    }

    public static string? Read(string appName, string valueName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\{appName}");
            return key?.GetValue(valueName) as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    /// <summary>Executable of a companion app: registry first, then the default per-user / machine install folders.</summary>
    public static string? FindExecutable(string appName)
    {
        var exe = appName + ".exe";
        var candidates = new[]
        {
            Read(appName, "ExePath"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", appName, exe),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), appName, exe),
        };
        return candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
    }
}
