using System.Diagnostics;

namespace OpenLauncher.UI.Services;

public interface IShellService
{
    /// <returns><c>false</c> if the folder does not exist or Explorer could not be started.</returns>
    bool OpenFolder(string path);
}

public sealed class ShellService : IShellService
{
    public bool OpenFolder(string path)
    {
        if (!Directory.Exists(path)) return false;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path }, UseShellExecute = false });
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
