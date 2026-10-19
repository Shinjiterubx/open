using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenCalendar.UI.ViewModels;

namespace OpenCalendar.UI.Views;

/// <summary>Name and colour of a calendar.</summary>
public partial class CalendarDialog : Window
{
    private readonly CalendarDialogViewModel _viewModel;

    public CalendarDialog(CalendarDialogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => NameInput.Focus();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            DialogResult = false;
            e.Handled = true;
        };
    }

    private void OnSwatchLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string color } swatch && string.Equals(color, _viewModel.Color, StringComparison.OrdinalIgnoreCase))
            swatch.IsChecked = true;
    }

    private void OnSwatchChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string color }) _viewModel.Color = color;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.Name))
        {
            NameInput.Focus();
            return;
        }
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnDragArea(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not TextBox) DragMove();
    }
}
