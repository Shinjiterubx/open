using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using OpenNotes.Core.Abstractions;
using OpenNotes.Core.Models;

namespace OpenNotes.Infrastructure.Hotkeys;

/// <summary>
/// Win32 <c>RegisterHotKey</c> based global shortcuts, received by a hidden message-only window.
/// Must be created and used on the UI thread.
/// </summary>
public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 0xB000;

    public GlobalHotkeyService()
    {
        var parameters = new HwndSourceParameters("openNotes.Hotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = HwndMessage,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public bool TryRegister(HotkeyGesture gesture, Action handler)
    {
        if (!gesture.IsValid || !Enum.TryParse<Key>(gesture.Key, out var key)) return false;

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0) return false;

        var id = _nextId++;
        if (!RegisterHotKey(_source.Handle, id, (uint)gesture.Modifiers | ModNoRepeat, virtualKey))
            return false;

        _handlers[id] = handler;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _handlers.Keys)
            UnregisterHotKey(_source.Handle, id);
        _handlers.Clear();
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && _handlers.TryGetValue(wParam.ToInt32(), out var handler))
        {
            handler();
            handled = true;
        }
        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
