using System.Diagnostics;
using Microsoft.Win32;
using OpenCalendar.Core.Abstractions;

namespace OpenCalendar.Infrastructure.Integration;

/// <summary>Opens a card with <c>openKanban.exe --open-card {id}</c> (a running instance receives it).</summary>
public sealed class OpenKanbanLauncher : IKanbanLauncher
{
    public bool TryOpenCard(Guid cardId)
    {
        var exe = FindExecutable();
        if (exe is null) return false;

        try
        {
            var startInfo = new ProcessStartInfo(exe) { UseShellExecute = false };
            startInfo.ArgumentList.Add("--open-card");
            startInfo.ArgumentList.Add(cardId.ToString("N"));
            Process.Start(startInfo);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }

    private static string? FindExecutable()
    {
        string? fromRegistry = null;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\openKanban");
            fromRegistry = key?.GetValue("ExePath") as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // fall back to default install folders
        }

        var candidates = new[]
        {
            fromRegistry,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "openKanban", "openKanban.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "openKanban", "openKanban.exe"),
        };
        return candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p));
    }
}
