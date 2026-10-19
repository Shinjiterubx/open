using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OpenKanban.UI.Views;

/// <summary>Shared behaviour of the small borderless dialogs: drag to move, Esc = cancel, Enter = OK.</summary>
internal static class DialogShell
{
    public static void Attach(Window window, FrameworkElement dragSurface, Func<bool>? validate = null)
    {
        dragSurface.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not TextBox) window.DragMove();
        };

        window.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                window.DialogResult = false;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && Keyboard.FocusedElement is not TextBox { AcceptsReturn: true })
            {
                (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
                if (validate is null || validate()) window.DialogResult = true;
                e.Handled = true;
            }
        };
    }
}
