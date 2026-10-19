using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace OpenKanban.UI.Controls;

/// <summary><c>FocusBehavior.IsFocused="{Binding ...}"</c>: focuses the element whenever the value turns true (and when loaded while true).</summary>
public static class FocusBehavior
{
    public static readonly DependencyProperty IsFocusedProperty = DependencyProperty.RegisterAttached(
        "IsFocused", typeof(bool), typeof(FocusBehavior), new PropertyMetadata(false, OnChanged));

    public static bool GetIsFocused(DependencyObject d) => (bool)d.GetValue(IsFocusedProperty);
    public static void SetIsFocused(DependencyObject d, bool value) => d.SetValue(IsFocusedProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element || e.NewValue is not true) return;

        void Focus() => element.Dispatcher.InvokeAsync(() =>
        {
            element.Focus();
            if (element is TextBox box) box.CaretIndex = box.Text.Length;
        }, DispatcherPriority.Input);

        if (element.IsLoaded) Focus();
        else element.Loaded += (_, _) => { if (GetIsFocused(element)) Focus(); };
    }
}
