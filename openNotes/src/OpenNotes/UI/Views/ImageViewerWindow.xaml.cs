using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace OpenNotes.UI.Views;

/// <summary>Full-screen lightbox for an embedded image: zoom (wheel, buttons, keys), pan, copy, save.</summary>
public partial class ImageViewerWindow : Window
{
    private const double MinZoom = 0.1;
    private const double MaxZoom = 12;
    private const double ZoomStep = 1.2;

    private readonly byte[] _data;
    private readonly string _fileName;
    private readonly BitmapSource _bitmap;

    private Point _dragStart;
    private Point _panStart;
    private bool _dragging;

    private ImageViewerWindow(byte[] data, string fileName, BitmapSource bitmap)
    {
        InitializeComponent();
        _data = data;
        _fileName = fileName;
        _bitmap = bitmap;

        Picture.Source = bitmap;
        FileNameText.Text = fileName;
        InfoText.Text = $"{bitmap.PixelWidth} × {bitmap.PixelHeight} px  ·  {FormatSize(data.Length)}";
        UpdateZoomLabel();

        SourceInitialized += (_, _) => WindowState = WindowState.Maximized;
        PreviewKeyDown += OnKeyDown;
        PreviewMouseWheel += OnMouseWheel;

        Picture.SizeChanged += (_, _) => UpdateZoomLabel();
        Picture.MouseLeftButtonDown += OnPictureMouseDown;
        Picture.MouseMove += OnPictureMouseMove;
        Picture.MouseLeftButtonUp += OnPictureMouseUp;
        Stage.MouseLeftButtonUp += (_, e) =>
        {
            // Click on the dark backdrop (not on the picture) closes the viewer.
            if (ReferenceEquals(e.OriginalSource, Stage)) Close();
        };
    }

    /// <summary>
    /// Creates a viewer on the owner's monitor. Formats WPF can't decode (e.g. SVG) are opened
    /// with the default Windows app instead, and <c>null</c> is returned.
    /// </summary>
    public static ImageViewerWindow? TryCreate(byte[] data, string fileName, Window? owner)
    {
        BitmapSource bitmap;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(data, writable: false);
            image.EndInit();
            image.Freeze();
            bitmap = image;
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException or IOException)
        {
            OpenWithDefaultApp(data, fileName);
            return null;
        }

        var viewer = new ImageViewerWindow(data, fileName, bitmap);
        if (owner is { IsVisible: true })
        {
            viewer.Owner = owner;
            // Start on the owner's monitor; SourceInitialized then maximizes it there.
            viewer.Width = 200;
            viewer.Height = 200;
            viewer.Left = owner.Left + owner.ActualWidth / 2 - 100;
            viewer.Top = owner.Top + owner.ActualHeight / 2 - 100;
        }
        return viewer;
    }

    // ───────────────────────── zoom & pan ─────────────────────────

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var factor = e.Delta > 0 ? ZoomStep : 1 / ZoomStep;
        ZoomAround(e.GetPosition(Picture), factor);
        e.Handled = true;
    }

    /// <param name="anchor">Point in the picture's own coordinates that should stay under the cursor.</param>
    private void ZoomAround(Point anchor, double factor)
    {
        var oldScale = Zoom.ScaleX;
        var newScale = Math.Clamp(oldScale * factor, MinZoom, MaxZoom);
        if (Math.Abs(newScale - oldScale) < 0.0001) return;

        Pan.X += (oldScale - newScale) * anchor.X;
        Pan.Y += (oldScale - newScale) * anchor.Y;
        Zoom.ScaleX = Zoom.ScaleY = newScale;
        UpdateZoomLabel();
    }

    private void ZoomAroundCenter(double factor) =>
        ZoomAround(new Point(Picture.ActualWidth / 2, Picture.ActualHeight / 2), factor);

    private void ResetZoom()
    {
        Zoom.ScaleX = Zoom.ScaleY = 1;
        Pan.X = Pan.Y = 0;
        UpdateZoomLabel();
    }

    private void UpdateZoomLabel()
    {
        // Percentage relative to the image's real pixel size.
        var fitted = Picture.ActualWidth > 0 && _bitmap.PixelWidth > 0 ? Picture.ActualWidth / _bitmap.PixelWidth : 1;
        ZoomLabel.Content = $"{Math.Round(fitted * Zoom.ScaleX * 100)}%";
    }

    private void OnPictureMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (e.ClickCount == 2)
        {
            ResetZoom();
            return;
        }

        _dragging = true;
        _dragStart = e.GetPosition(Stage);
        _panStart = new Point(Pan.X, Pan.Y);
        Picture.CaptureMouse();
    }

    private void OnPictureMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        var position = e.GetPosition(Stage);
        Pan.X = _panStart.X + (position.X - _dragStart.X);
        Pan.Y = _panStart.Y + (position.Y - _dragStart.Y);
    }

    private void OnPictureMouseUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        Picture.ReleaseMouseCapture();
        e.Handled = true;
    }

    // ───────────────────────── commands ─────────────────────────

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        switch (e.Key)
        {
            case Key.Escape: Close(); break;
            case Key.OemPlus or Key.Add: ZoomAroundCenter(ZoomStep); break;
            case Key.OemMinus or Key.Subtract: ZoomAroundCenter(1 / ZoomStep); break;
            case Key.D0 or Key.NumPad0: ResetZoom(); break;
            case Key.C when ctrl: Copy(); break;
            case Key.S when ctrl: Save(); break;
            default: return;
        }
        e.Handled = true;
    }

    private void OnZoomIn(object sender, RoutedEventArgs e) => ZoomAroundCenter(ZoomStep);

    private void OnZoomOut(object sender, RoutedEventArgs e) => ZoomAroundCenter(1 / ZoomStep);

    private void OnResetZoom(object sender, RoutedEventArgs e) => ResetZoom();

    private void OnCopy(object sender, RoutedEventArgs e) => Copy();

    private void OnSave(object sender, RoutedEventArgs e) => Save();

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void Copy()
    {
        try
        {
            Clipboard.SetImage(_bitmap);
            InfoText.Text = "Kép a vágólapra másolva";
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            InfoText.Text = "A vágólap foglalt, próbáld újra";
        }
    }

    private void Save()
    {
        var extension = Path.GetExtension(_fileName);
        var dialog = new SaveFileDialog
        {
            Title = "Kép mentése",
            FileName = _fileName,
            Filter = $"Kép (*{extension})|*{extension}|Minden fájl|*.*",
            OverwritePrompt = true,
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            File.WriteAllBytes(dialog.FileName, _data);
            InfoText.Text = "Mentve: " + dialog.FileName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InfoText.Text = "Mentés sikertelen: " + ex.Message;
        }
    }

    private static void OpenWithDefaultApp(byte[] data, string fileName)
    {
        var directory = Path.Combine(Path.GetTempPath(), "openNotes");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Path.GetFileName(fileName));
        File.WriteAllBytes(path, data);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024):0.#} MB",
    };
}
