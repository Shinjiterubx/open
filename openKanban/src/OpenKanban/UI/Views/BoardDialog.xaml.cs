using System.Windows;
using OpenKanban.UI.ViewModels;

namespace OpenKanban.UI.Views;

public partial class BoardDialog : Window
{
    private readonly BoardDialogViewModel _viewModel;

    public BoardDialog(BoardDialogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        DialogShell.Attach(this, Surface, Validate);
        Loaded += (_, _) =>
        {
            NameInput.Focus();
            NameInput.SelectAll();
        };
    }

    private bool Validate()
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.Name)) return true;
        NameInput.Focus();
        return false;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (Validate()) DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
