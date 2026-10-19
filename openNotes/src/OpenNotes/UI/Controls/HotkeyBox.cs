using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenNotes.Core.Models;

namespace OpenNotes.UI.Controls;

/// <summary>Click and press a key combination to record a global hotkey.</summary>
public sealed class HotkeyBox : TextBox
{
    public static readonly DependencyProperty GestureProperty = DependencyProperty.Register(
        nameof(Gesture), typeof(HotkeyGesture), typeof(HotkeyBox),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((HotkeyBox)d).ShowGesture()));

    public HotkeyBox()
    {
        IsReadOnly = true;
        IsUndoEnabled = false;
        ContextMenu = null;
    }

    public HotkeyGesture? Gesture
    {
        get => (HotkeyGesture?)GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        Text = "Nyomd le a billentyűkombinációt…";
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        ShowGesture();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        if (key == Key.Tab && modifiers == ModifierKeys.None) return;   // keep keyboard navigation
        e.Handled = true;

        if (key == Key.Escape)
        {
            ShowGesture();
            Keyboard.ClearFocus();
            return;
        }

        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.ImeProcessed or Key.DeadCharProcessed)
        {
            Text = new HotkeyGesture((HotkeyModifiers)(int)modifiers, string.Empty) + " + …";
            return;
        }

        if (modifiers == ModifierKeys.None || modifiers == ModifierKeys.Shift)
        {
            Text = "Használj Ctrl, Alt vagy Win módosítót";
            return;
        }

        SetCurrentValue(GestureProperty, new HotkeyGesture((HotkeyModifiers)(int)modifiers, key.ToString()));
        ShowGesture();
    }

    private void ShowGesture() => Text = Gesture?.ToString() ?? "Nincs beállítva";
}
