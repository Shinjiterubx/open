using System.Globalization;

namespace OpenKanban.UI.ViewModels;

/// <summary>Hungarian formatting helpers.</summary>
internal static class Hu
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("hu-HU");

    public static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], Culture) + text[1..];
}
