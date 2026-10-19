using System.Diagnostics;
using Microsoft.Win32;
using OpenLauncher.Core.Abstractions;
using OpenLauncher.Core.Models;

namespace OpenLauncher.Infrastructure.Installation;

/// <summary>
/// Works with the apps' Inno Setup installers:
/// <list type="bullet">
/// <item>detection – the uninstall entry <c>…\Uninstall\{AppId}_is1</c> (per-user HKCU, or per-machine HKLM),
///       falling back to <c>HKCU\Software\{app}\ExePath</c> for portable copies;</item>
/// <item>install – runs the bundled setup silently for the current user (no admin rights needed);</item>
/// <item>uninstall – runs the app's own uninstaller silently.</item>
/// </list>
/// </summary>
public sealed class InnoSetupInstallationService : IInstallationService
{
    private const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
    private static readonly TimeSpan UninstallTimeout = TimeSpan.FromMinutes(3);

    private readonly IAppPaths _paths;

    public InnoSetupInstallationService(IAppPaths paths) => _paths = paths;

    public AppStatus GetStatus(AppDefinition app)
    {
        var installerPath = InstallerPath(app);
        var installerAvailable = File.Exists(installerPath);
        var installerVersion = installerAvailable ? FileVersionInfo.GetVersionInfo(installerPath).ProductVersion : null;
        var running = IsRunning(app);

        if (FindUninstallEntry(app) is { } entry)
        {
            var location = entry.InstallLocation;
            var exe = location is null ? null : Path.Combine(location, app.ExeName);
            if (exe is not null && !File.Exists(exe)) exe = null;
            return new AppStatus(true, entry.Version, location, exe, entry.Uninstaller, running, installerAvailable, installerVersion);
        }

        // Not installed by a setup, but maybe a portable copy that published its location.
        var published = ReadPublishedExe(app);
        return published is not null
            ? new AppStatus(false, null, Path.GetDirectoryName(published), published, null, running, installerAvailable, installerVersion)
            : AppStatus.NotInstalled(installerAvailable, installerVersion) with { IsRunning = running };
    }

    public async Task<OperationResult> InstallAsync(AppDefinition app, CancellationToken cancellationToken = default)
    {
        var installer = InstallerPath(app);
        if (!File.Exists(installer))
            return OperationResult.Fail($"A telepítő nem található:\n{installer}");

        var log = Path.Combine(_paths.LogDirectory, $"install-{app.Id}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        var startInfo = new ProcessStartInfo(installer) { UseShellExecute = false };
        foreach (var argument in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CURRENTUSER", "/SP-", $"/LOG={log}" })
            startInfo.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("A telepítő nem indult el.");
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0
                ? OperationResult.Ok
                : OperationResult.Fail($"A telepítő hibakóddal állt le ({process.ExitCode}).\nNapló: {log}");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public async Task<OperationResult> UninstallAsync(AppDefinition app, CancellationToken cancellationToken = default)
    {
        if (FindUninstallEntry(app) is not { Uninstaller: { } uninstaller })
            return OperationResult.Fail("Az alkalmazás nincs telepítve (vagy nem telepítővel került fel).");
        if (!File.Exists(uninstaller))
            return OperationResult.Fail($"Az eltávolító nem található:\n{uninstaller}");

        var startInfo = new ProcessStartInfo(uninstaller) { UseShellExecute = false };
        foreach (var argument in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART" })
            startInfo.ArgumentList.Add(argument);

        try
        {
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Az eltávolító nem indult el.");
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return OperationResult.Fail(ex.Message);
        }

        // The Inno uninstaller re-launches itself from %TEMP% and returns at once: wait for the entry to disappear.
        var deadline = DateTime.UtcNow + UninstallTimeout;
        while (FindUninstallEntry(app) is not null)
        {
            if (DateTime.UtcNow > deadline) return OperationResult.Fail("Az eltávolítás nem fejeződött be időben.");
            await Task.Delay(500, cancellationToken);
        }
        return OperationResult.Ok;
    }

    public OperationResult Launch(AppDefinition app)
    {
        var exe = GetStatus(app).ExePath;
        if (exe is null) return OperationResult.Fail("Az alkalmazás nincs telepítve.");

        try
        {
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe) });
            return OperationResult.Ok;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    public bool StopRunning(AppDefinition app)
    {
        var allStopped = true;
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(app.ExeName)))
        {
            using (process)
            {
                try
                {
                    // The apps save on every change / on deactivate, so ending the process is safe.
                    process.Kill(entireProcessTree: true);
                    if (!process.WaitForExit(5000)) allStopped = false;
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    allStopped = false;
                }
            }
        }
        return allStopped;
    }

    // ───────────────────────── helpers ─────────────────────────

    private string InstallerPath(AppDefinition app) => Path.Combine(_paths.InstallersDirectory, app.InstallerFileName);

    private static bool IsRunning(AppDefinition app)
    {
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(app.ExeName));
        foreach (var process in processes) process.Dispose();
        return processes.Length > 0;
    }

    private sealed record UninstallEntry(string? Version, string? InstallLocation, string? Uninstaller);

    private static UninstallEntry? FindUninstallEntry(AppDefinition app)
    {
        var keyName = $@"{UninstallRoot}\{app.InnoAppId}_is1";
        var hives = new (RegistryHive Hive, RegistryView View)[]
        {
            (RegistryHive.CurrentUser, RegistryView.Default),
            (RegistryHive.LocalMachine, RegistryView.Registry64),
            (RegistryHive.LocalMachine, RegistryView.Registry32),
        };

        foreach (var (hive, view) in hives)
        {
            try
            {
                using var root = RegistryKey.OpenBaseKey(hive, view);
                using var key = root.OpenSubKey(keyName);
                if (key is null) continue;

                var location = (key.GetValue("InstallLocation") as string)?.TrimEnd('\\');
                var uninstall = key.GetValue("UninstallString") as string;
                return new UninstallEntry(key.GetValue("DisplayVersion") as string, location, ExtractPath(uninstall));
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                // try the next hive
            }
        }
        return null;
    }

    /// <summary>"\"C:\…\unins000.exe\"" → C:\…\unins000.exe</summary>
    internal static string? ExtractPath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            var end = command.IndexOf('"', 1);
            return end > 1 ? command[1..end] : null;
        }
        var exe = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe > 0 ? command[..(exe + 4)] : command;
    }

    private static string? ReadPublishedExe(AppDefinition app)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(app.RegistryKey);
            return key?.GetValue("ExePath") is string exe && File.Exists(exe) ? exe : null;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }
}
