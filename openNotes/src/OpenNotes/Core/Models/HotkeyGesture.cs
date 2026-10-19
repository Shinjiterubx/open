namespace OpenNotes.Core.Models;

/// <summary>Modifier flags. Values match the Win32 MOD_* constants (and WPF ModifierKeys).</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

/// <summary>
/// A system-wide keyboard shortcut. <see cref="Key"/> holds the WPF <c>System.Windows.Input.Key</c>
/// name (e.g. "N", "D1", "Space") so settings stay human readable.
/// </summary>
public sealed record HotkeyGesture(HotkeyModifiers Modifiers, string Key)
{
    /// <summary>Ctrl + Alt + Shift + O – not used by Windows.</summary>
    public static HotkeyGesture DefaultOpenLastNote { get; } =
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, "O");

    /// <summary>Ctrl + Alt + Shift + N – not used by Windows.</summary>
    public static HotkeyGesture DefaultNewNote { get; } =
        new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, "N");

    public bool IsValid => Modifiers != HotkeyModifiers.None && !string.IsNullOrWhiteSpace(Key);

    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
        if (!string.IsNullOrWhiteSpace(Key)) parts.Add(FormatKey(Key));
        return string.Join(" + ", parts);
    }

    private static string FormatKey(string key) => key switch
    {
        _ when key.Length == 2 && key[0] == 'D' && char.IsDigit(key[1]) => key[1].ToString(),
        _ when key.StartsWith("NumPad", StringComparison.Ordinal) => "Num " + key[6..],
        "Space" => "Szóköz",
        "Return" or "Enter" => "Enter",
        "OemComma" => ",",
        "OemPeriod" => ".",
        "OemMinus" => "-",
        "OemPlus" => "+",
        _ => key,
    };
}
