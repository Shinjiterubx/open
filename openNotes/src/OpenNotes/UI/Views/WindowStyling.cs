using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace OpenNotes.UI.Views;

/// <summary>
/// Small helpers shared by the custom-chrome windows, including keeping every window usable at any
/// display scaling (125 %, 150 %, 200 % …) and screen size – see <see cref="EnableScreenFitting"/>.
/// </summary>
internal static class WindowStyling
{
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpRound = 2;
    private const int MonitorDefaultToNearest = 2;

    /// <summary>Gap kept between a window and the edge of the work area (DIPs).</summary>
    private const double ScreenMargin = 8;

    private static readonly DependencyProperty IsScreenFittingAttachedProperty =
        DependencyProperty.RegisterAttached("IsScreenFittingAttached", typeof(bool), typeof(WindowStyling), new PropertyMetadata(false));

    /// <summary>The window's minimum size as designed in XAML (before any fitting).</summary>
    private static readonly DependencyProperty DesignMinSizeProperty =
        DependencyProperty.RegisterAttached("DesignMinSize", typeof(Size?), typeof(WindowStyling), new PropertyMetadata(null));

    // ───────────────────────── screen fitting ─────────────────────────

    /// <summary>
    /// Call once at start-up. From then on every window of the app is fitted to the monitor it opens on:
    /// <list type="bullet">
    /// <item>size and minimum size never exceed the work area (taskbar excluded), so title bar and buttons stay reachable;</item>
    /// <item>auto-sized dialogs that would be taller than the screen are scaled down to fit;</item>
    /// <item>the window is moved fully onto the screen;</item>
    /// <item>this is repeated when the window moves to a monitor with a different scaling.</item>
    /// </list>
    /// </summary>
    public static void EnableScreenFitting() =>
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window window) AttachScreenFitting(window);
            }));

    private static void AttachScreenFitting(Window window)
    {
        if ((bool)window.GetValue(IsScreenFittingAttachedProperty)) return;
        window.SetValue(IsScreenFittingAttachedProperty, true);

        FitToScreen(window);
        window.DpiChanged += (_, _) =>
            window.Dispatcher.InvokeAsync(() => FitToScreen(window), DispatcherPriority.Loaded);
    }

    /// <summary>Fits a window into the work area of its monitor. Safe to call repeatedly.</summary>
    public static void FitToScreen(Window window)
    {
        var area = WorkAreaOf(window);
        if (area.IsEmpty || area.Width < 100 || area.Height < 100) return;

        var maxWidth = area.Width - 2 * ScreenMargin;
        var maxHeight = area.Height - 2 * ScreenMargin;

        if (window.SizeToContent == SizeToContent.Manual) ScaleLayoutToFit(window, maxWidth, maxHeight);
        if (window.WindowState != WindowState.Normal) return;

        if (window.SizeToContent == SizeToContent.Manual)
        {
            if (window.ActualWidth > maxWidth || window.Width > maxWidth) window.Width = maxWidth;
            if (window.ActualHeight > maxHeight || window.Height > maxHeight) window.Height = maxHeight;
        }
        else
        {
            ScaleContentToFit(window, maxWidth, maxHeight);
        }

        window.UpdateLayout();
        var width = window.ActualWidth;
        var height = window.ActualHeight;

        var left = double.IsNaN(window.Left) ? area.Left + (area.Width - width) / 2 : window.Left;
        var top = double.IsNaN(window.Top) ? area.Top + (area.Height - height) / 2 : window.Top;

        left = Clamp(left, area.Left + ScreenMargin, area.Right - ScreenMargin - width);
        top = Clamp(top, area.Top + ScreenMargin, area.Bottom - ScreenMargin - height);

        if (!AreClose(left, window.Left)) window.Left = left;
        if (!AreClose(top, window.Top)) window.Top = top;
    }

    /// <summary>
    /// Resizable windows are designed for a minimum size (e.g. 960 × 600). If the screen is smaller than that
    /// (small laptop at 150–200 % scaling), the whole layout is scaled down uniformly instead of being squeezed,
    /// so nothing overlaps or gets cut off. On larger screens the scale is 1 (no change).
    /// </summary>
    private static void ScaleLayoutToFit(Window window, double maxWidth, double maxHeight)
    {
        if (window.Content is not FrameworkElement content) return;

        if (window.GetValue(DesignMinSizeProperty) is not Size design)
        {
            design = new Size(window.MinWidth, window.MinHeight);
            window.SetValue(DesignMinSizeProperty, design);
        }

        var scale = 1.0;
        if (design.Width > maxWidth && design.Width > 0) scale = Math.Min(scale, maxWidth / design.Width);
        if (design.Height > maxHeight && design.Height > 0) scale = Math.Min(scale, maxHeight / design.Height);
        scale = Math.Max(scale, 0.5);

        content.LayoutTransform = scale < 0.999 ? new ScaleTransform(scale, scale) : Transform.Identity;
        window.MinWidth = Math.Min(design.Width * scale, maxWidth);
        window.MinHeight = Math.Min(design.Height * scale, maxHeight);
    }

    /// <summary>
    /// Auto-sized dialogs have no scroll bar: if one is larger than the screen, its content is scaled down
    /// (a LayoutTransform re-flows text, so it stays crisp and fully visible).
    /// </summary>
    private static void ScaleContentToFit(Window window, double maxWidth, double maxHeight)
    {
        if (window.Content is not FrameworkElement content) return;

        content.LayoutTransform = Transform.Identity;
        window.UpdateLayout();

        var scale = Math.Min(1.0, Math.Min(maxHeight / Math.Max(1, window.ActualHeight), maxWidth / Math.Max(1, window.ActualWidth)));
        if (scale >= 0.999) return;

        content.LayoutTransform = new ScaleTransform(scale, scale);
        if (window.SizeToContent == SizeToContent.Height && !double.IsNaN(window.Width))
            window.Width = Math.Min(window.Width, maxWidth);
    }

    /// <summary>Work area (screen without the taskbar) of the monitor the window is on, in WPF units (DIPs).</summary>
    public static Rect WorkAreaOf(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var monitor = handle != IntPtr.Zero
            ? MonitorFromWindow(handle, MonitorDefaultToNearest)
            : GetCursorPos(out var cursor) ? MonitorFromPoint(cursor, MonitorDefaultToNearest) : IntPtr.Zero;

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info)) return SystemParameters.WorkArea;

        var fromDevice = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        var topLeft = fromDevice.Transform(new Point(info.Work.Left, info.Work.Top));
        var bottomRight = fromDevice.Transform(new Point(info.Work.Right, info.Work.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    // ───────────────────────── chrome ─────────────────────────

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

    /// <summary>
    /// A maximized chrome-less window overhangs the screen by the (DPI dependent) resize frame.
    /// Pads the content back in by exactly the overhang, measured against the monitor's work area,
    /// so the title bar and the status bar are fully visible at every scaling.
    /// </summary>
    public static void KeepContentOnScreenWhenMaximized(Window window, FrameworkElement root)
    {
        void Update()
        {
            if (window.WindowState != WindowState.Maximized)
            {
                root.Margin = new Thickness(0);
                return;
            }

            var handle = new WindowInteropHelper(window).Handle;
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (handle == IntPtr.Zero || !GetWindowRect(handle, out var bounds)
                || !GetMonitorInfo(MonitorFromWindow(handle, MonitorDefaultToNearest), ref info))
            {
                var frame = SystemParameters.WindowResizeBorderThickness;
                root.Margin = new Thickness(frame.Left + 4, frame.Top + 4, frame.Right + 4, frame.Bottom + 4);
                return;
            }

            var dpi = VisualTreeHelper.GetDpi(window);
            root.Margin = new Thickness(
                Math.Max(0, info.Work.Left - bounds.Left) / dpi.DpiScaleX,
                Math.Max(0, info.Work.Top - bounds.Top) / dpi.DpiScaleY,
                Math.Max(0, bounds.Right - info.Work.Right) / dpi.DpiScaleX,
                Math.Max(0, bounds.Bottom - info.Work.Bottom) / dpi.DpiScaleY);
        }

        window.StateChanged += (_, _) => Update();
        window.SizeChanged += (_, _) => Update();
        window.DpiChanged += (_, _) => window.Dispatcher.InvokeAsync(Update, DispatcherPriority.Loaded);
        window.SourceInitialized += (_, _) => Update();
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
        // Size / position are checked against the actual monitor once the window is loaded (screen fitting).
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

    // ───────────────────────── helpers / interop ─────────────────────────

    /// <summary>Like Math.Clamp, but tolerates max &lt; min (window larger than the area → align to min).</summary>
    private static double Clamp(double value, double min, double max) => max < min ? min : Math.Min(Math.Max(value, min), max);

    private static bool AreClose(double a, double b) => !double.IsNaN(b) && Math.Abs(a - b) < 0.5;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
}
