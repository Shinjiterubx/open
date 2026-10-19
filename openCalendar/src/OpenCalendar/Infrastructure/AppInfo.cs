using System.Reflection;

namespace OpenCalendar.Infrastructure;

public static class AppInfo
{
    public const string Name = "openCalendar";

    public static string Version { get; } =
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
        ?? typeof(AppInfo).Assembly.GetName().Version?.ToString(3)
        ?? "1.0.0";
}
