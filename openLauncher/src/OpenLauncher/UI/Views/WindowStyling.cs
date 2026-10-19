using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OpenLauncher.UI.Views;

/// <summary>Small helpers shared by the custom-chrome windows.</summary>
internal static class WindowStyling
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;

    /// <summary>Asks Windows 11 for rounded corners (no-op on Windows 10).</summary>
    public static void ApplyRoundedCorners(Window window)
    {
        void Apply()
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            var preference = DwmwcpRound;
            _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref preference, sizeof(int));
        }

        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) Apply();
        else window.SourceInitialized += (_, _) => Apply();
    }

    /// <summary>A maximized chrome-less window overhangs the screen by the resize border; pad the content back in.</summary>
    public static void KeepContentOnScreenWhenMaximized(Window window, FrameworkElement root)
    {
        void Update() => root.Margin = window.WindowState == WindowState.Maximized
            ? new Thickness(SystemParameters.WindowResizeBorderThickness.Left + 4,
                            SystemParameters.WindowResizeBorderThickness.Top + 4,
                            SystemParameters.WindowResizeBorderThickness.Right + 4,
                            SystemParameters.WindowResizeBorderThickness.Bottom + 4)
            : new Thickness(0);

        window.StateChanged += (_, _) => Update();
        Update();
    }

    public static void BringToFront(Window window)
    {
        if (!window.IsVisible) window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;

        window.Activate();
        // Topmost toggle is the reliable way to come to the front after a global hotkey.
        if (!window.Topmost)
        {
            window.Topmost = true;
            window.Topmost = false;
        }
        window.Focus();
    }

    /// <returns><c>true</c> if valid, on-screen bounds were applied.</returns>
    public static bool RestoreBounds(Window window, Core.Models.WindowBounds? bounds)
    {
        if (bounds is null || bounds.Width < 200 || bounds.Height < 150) return false;

        var visible = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var target = new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
        if (!visible.IntersectsWith(target)) return false;

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = bounds.Left;
        window.Top = bounds.Top;
        window.Width = bounds.Width;
        window.Height = bounds.Height;
        if (bounds.IsMaximized) window.WindowState = WindowState.Maximized;
        return true;
    }

    public static Core.Models.WindowBounds CaptureBounds(Window window)
    {
        var rect = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;
        return new Core.Models.WindowBounds
        {
            Left = rect.Left,
            Top = rect.Top,
            Width = rect.Width,
            Height = rect.Height,
            IsMaximized = window.WindowState == WindowState.Maximized,
        };
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
