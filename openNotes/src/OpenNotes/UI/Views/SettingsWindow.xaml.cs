using System.Windows;
using System.Windows.Input;
using OpenNotes.UI.ViewModels;

namespace OpenNotes.UI.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        WindowStyling.ApplyRoundedCorners(this);
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || Keyboard.FocusedElement is Controls.HotkeyBox) return;
            DialogResult = false;
            e.Handled = true;
        };
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.Save()) DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
