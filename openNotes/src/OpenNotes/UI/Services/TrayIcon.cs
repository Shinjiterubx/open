using System.Windows;
using OpenNotes.UI.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace OpenNotes.UI.Services;

/// <summary>Notification area icon: keeps the app reachable while running in the background.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private Action? _balloonClickAction;

    public TrayIcon(IWindowService windows, MainViewModel main, IAppLifetime lifetime)
    {
        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new Forms.ToolStripProfessionalRenderer(new TrayColors()),
            Font = new Drawing.Font("Segoe UI", 9.5f),
            ShowImageMargin = false,
            Padding = new Forms.Padding(4),
        };
        menu.Items.Add("openNotes megnyitása", null, (_, _) => windows.ShowMain());
        menu.Items.Add("Új jegyzet", null, (_, _) => { main.NewNote(); windows.ShowQuickWindow(focusTitle: true); });
        menu.Items.Add("Mini ablak", null, (_, _) => windows.ShowMini());
        menu.Items.Add("Teendők összegzése", null, (_, _) => windows.ShowReminderSummary());
        menu.Items.Add("Beállítások", null, (_, _) => windows.ShowSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Kilépés", null, (_, _) => lifetime.RequestExit());

        _icon = new Forms.NotifyIcon
        {
            Text = "openNotes",
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Visible = false,
        };
        _icon.BalloonTipClicked += (_, _) => _balloonClickAction?.Invoke();
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) windows.ShowLastUsed();
        };
    }

    public void Show() => _icon.Visible = true;

    public void Notify(string title, string text) => Notify(title, text, isWarning: true, onClick: null);

    /// <summary>Shows a Windows notification; <paramref name="onClick"/> runs when the user clicks it.</summary>
    public void Notify(string title, string text, bool isWarning, Action? onClick)
    {
        _balloonClickAction = onClick;
        _icon.ShowBalloonTip(6000, title, text, isWarning ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }

    private static Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/openNotes.ico"))
                       ?? throw new InvalidOperationException("Hiányzó ikon erőforrás.");
        using var stream = resource.Stream;
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }

    private sealed class TrayColors : Forms.ProfessionalColorTable
    {
        private static readonly Drawing.Color Soft = Drawing.Color.FromArgb(0xEF, 0xE9, 0xFA);
        private static readonly Drawing.Color Accent = Drawing.Color.FromArgb(0x5B, 0x3E, 0x96);

        public override Drawing.Color MenuItemSelected => Soft;
        public override Drawing.Color MenuItemBorder => Soft;
        public override Drawing.Color MenuBorder => Drawing.Color.FromArgb(0xDA, 0xD3, 0xE6);
        public override Drawing.Color ToolStripDropDownBackground => Drawing.Color.White;
        public override Drawing.Color SeparatorDark => Drawing.Color.FromArgb(0xEC, 0xE8, 0xF2);
        public override Drawing.Color MenuItemPressedGradientBegin => Accent;
        public override Drawing.Color MenuItemPressedGradientEnd => Accent;
    }
}
