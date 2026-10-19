namespace OpenLauncher.Core.Versioning;

/// <summary>Compares loosely formatted version strings ("1.2", "1.2.0", "1.2.0+abc").</summary>
public static class VersionText
{
    public static Version? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var core = text.Trim().Split('+', '-', ' ')[0];
        if (!Version.TryParse(core, out var version)) return null;
        // Normalise missing parts to 0 so "1.2" == "1.2.0".
        return new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
    }

    /// <returns><c>true</c> if <paramref name="candidate"/> is a strictly newer version than <paramref name="current"/>.</returns>
    public static bool IsNewer(string? candidate, string? current)
    {
        var a = Parse(candidate);
        var b = Parse(current);
        return a is not null && b is not null && a > b;
    }

    /// <summary>"1.0.0.0" → "1.0.0" for display.</summary>
    public static string? Display(string? text) =>
        Parse(text) is { } v ? (v.Revision > 0 ? v.ToString(4) : v.ToString(3)) : text;
}
