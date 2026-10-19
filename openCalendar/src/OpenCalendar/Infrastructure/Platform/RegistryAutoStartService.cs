using Microsoft.Win32;
using OpenCalendar.Core.Abstractions;

namespace OpenCalendar.Infrastructure.Platform;

/// <summary>Per-user autostart via <c>HKCU\...\Run</c> (no admin rights needed; the installer uses the same value).</summary>
public sealed class RegistryAutoStartService : IAutoStartService
{
    public const string BackgroundArgument = "--background";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "openCalendar";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled)
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Az alkalmazás elérési útja nem állapítható meg.");
            key.SetValue(ValueName, $"\"{exe}\" {BackgroundArgument}");
        }
        else if (key.GetValue(ValueName) is not null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
