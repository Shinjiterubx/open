using System.Windows;
using OpenCalendar.UI.ViewModels;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace OpenCalendar.UI.Services;

/// <summary>Notification area icon: keeps the calendar (and its reminders) running in the background.</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;

    public TrayIcon(IWindowService windows, MainViewModel main, IAppLifetime lifetime)
    {
        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new Forms.ToolStripProfessionalRenderer(new TrayColors()),
            Font = new Drawing.Font("Segoe UI", 9.5f),
            ShowImageMargin = false,
            Padding = new Forms.Padding(4),
        };
        menu.Items.Add("openCalendar megnyitása", null, (_, _) => windows.ShowMain());
        menu.Items.Add("Új esemény", null, (_, _) => { windows.ShowMain(); main.NewEventCommand.Execute(null); });
        menu.Items.Add("Ma", null, (_, _) => { main.GoTodayCommand.Execute(null); windows.ShowMain(); });
        menu.Items.Add("Beállítások", null, (_, _) => windows.ShowSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Kilépés", null, (_, _) => lifetime.RequestExit());

        _icon = new Forms.NotifyIcon
        {
            Text = "openCalendar",
            Icon = LoadIcon(),
            ContextMenuStrip = menu,
            Visible = false,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) windows.ShowMain();
        };
    }

    public void Show() => _icon.Visible = true;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.ContextMenuStrip?.Dispose();
        _icon.Dispose();
    }

    private static Drawing.Icon LoadIcon()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/openCalendar.ico"))
                       ?? throw new InvalidOperationException("Hiányzó ikon erőforrás.");
        using var stream = resource.Stream;
        return new Drawing.Icon(stream, Forms.SystemInformation.SmallIconSize);
    }

    private sealed class TrayColors : Forms.ProfessionalColorTable
    {
        private static readonly Drawing.Color Soft = Drawing.Color.FromArgb(0xFC, 0xE9, 0xEE);
        private static readonly Drawing.Color Accent = Drawing.Color.FromArgb(0xD4, 0x5D, 0x79);

        public override Drawing.Color MenuItemSelected => Soft;
        public override Drawing.Color MenuItemBorder => Soft;
        public override Drawing.Color MenuBorder => Drawing.Color.FromArgb(0xE6, 0xD3, 0xDA);
        public override Drawing.Color ToolStripDropDownBackground => Drawing.Color.White;
        public override Drawing.Color SeparatorDark => Drawing.Color.FromArgb(0xF1, 0xE6, 0xEA);
        public override Drawing.Color MenuItemPressedGradientBegin => Accent;
        public override Drawing.Color MenuItemPressedGradientEnd => Accent;
    }
}
