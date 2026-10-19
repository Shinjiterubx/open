using System.Windows;
using System.Windows.Input;
using OpenKanban.UI.ViewModels;

namespace OpenKanban.UI.Views;

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
            if (e.Key != Key.Escape) return;
            DialogResult = false;
            e.Handled = true;
        };
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Save();
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
