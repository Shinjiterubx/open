using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OpenNotes.UI.Services;

namespace OpenNotes.UI.Views;

/// <summary>Create / edit a folder: name and colour.</summary>
public partial class FolderDialog : Window
{
    private FolderDialogResult? _result;

    private FolderDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public static FolderDialogResult? Present(Window? owner, string title, string name, string color,
        IReadOnlyList<string> palette, string confirmText)
    {
        var dialog = new FolderDialog { Title = title };
        if (owner is { IsVisible: true }) dialog.Owner = owner;
        else dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        dialog.TitleText.Text = title;
        dialog.ConfirmButton.Content = confirmText;
        dialog.NameBox.Text = name;

        foreach (var swatch in palette.Contains(color, StringComparer.OrdinalIgnoreCase) ? palette : palette.Prepend(color))
        {
            var option = new RadioButton
            {
                Style = (Style)dialog.FindResource("FolderSwatch"),
                Tag = swatch,
                GroupName = "FolderColor",
                ToolTip = swatch,
                IsChecked = string.Equals(swatch, color, StringComparison.OrdinalIgnoreCase),
            };
            option.Checked += (_, _) => dialog.UpdateBadge();
            dialog.Swatches.Children.Add(option);
        }

        dialog.UpdateBadge();
        dialog.ShowDialog();
        return dialog._result;
    }

    private string SelectedColor =>
        Swatches.Children.OfType<RadioButton>().FirstOrDefault(r => r.IsChecked == true)?.Tag as string ?? "#5B3E96";

    private void UpdateBadge()
    {
        try
        {
            IconBadge.Background = (Brush)new BrushConverter().ConvertFromString(SelectedColor)!;
        }
        catch (FormatException)
        {
            IconBadge.Background = Brushes.Gray;
        }
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e) =>
        ConfirmButton.IsEnabled = !string.IsNullOrWhiteSpace(NameBox.Text);

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text)) return;
        _result = new FolderDialogResult(NameBox.Text.Trim(), SelectedColor);
        Close();
    }

    /// <summary>Drag the dialog by its background – but not when the press starts in the text box or on a button.</summary>
    private void OnDragArea(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        for (var element = e.OriginalSource as DependencyObject; element is not null;
             element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
        {
            if (element is TextBox or System.Windows.Controls.Primitives.ButtonBase) return;
            if (ReferenceEquals(element, sender)) break;
        }
        DragMove();
    }
}
