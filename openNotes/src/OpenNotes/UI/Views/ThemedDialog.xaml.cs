using System.Windows;
using System.Windows.Input;
using OpenNotes.UI.Services;

namespace OpenNotes.UI.Views;

/// <summary>Message / confirmation dialog in the app's visual style.</summary>
public partial class ThemedDialog : Window
{
    public ThemedDialog()
    {
        InitializeComponent();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            CloseWith(DialogChoice.Cancel);
            e.Handled = true;
        };
    }

    public DialogChoice Choice { get; private set; } = DialogChoice.Cancel;

    public static DialogChoice Present(Window? owner, string title, string message, string primaryText,
        string? secondaryText = null, string? cancelText = "Mégse", bool destructive = false, bool isError = false)
    {
        var dialog = new ThemedDialog { Title = title };
        if (owner is { IsVisible: true }) dialog.Owner = owner;
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        dialog.TitleText.Text = title;
        dialog.MessageText.Text = message;
        dialog.PrimaryButton.Content = primaryText;

        if (secondaryText is not null)
        {
            dialog.SecondaryButton.Content = secondaryText;
            dialog.SecondaryButton.Visibility = Visibility.Visible;
        }

        if (cancelText is null) dialog.CancelButton.Visibility = Visibility.Collapsed;
        else dialog.CancelButton.Content = cancelText;

        if (destructive || isError)
        {
            dialog.IconBadge.Background = (System.Windows.Media.Brush)dialog.FindResource("DangerSoftBrush");
            dialog.IconGlyph.Foreground = (System.Windows.Media.Brush)dialog.FindResource("DangerBrush");
            dialog.IconGlyph.Text = isError ? "" : "";
        }
        if (destructive) dialog.PrimaryButton.Style = (Style)dialog.FindResource("DangerButton");

        dialog.ShowDialog();
        return dialog.Choice;
    }

    private void OnPrimary(object sender, RoutedEventArgs e) => CloseWith(DialogChoice.Primary);

    private void OnSecondary(object sender, RoutedEventArgs e) => CloseWith(DialogChoice.Secondary);

    private void OnCancel(object sender, RoutedEventArgs e) => CloseWith(DialogChoice.Cancel);

    private void CloseWith(DialogChoice choice)
    {
        Choice = choice;
        Close();
    }

    private void OnDragArea(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }
}
