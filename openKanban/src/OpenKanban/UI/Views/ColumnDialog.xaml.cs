using System.Windows;
using OpenKanban.UI.ViewModels;

namespace OpenKanban.UI.Views;

public partial class ColumnDialog : Window
{
    private readonly ColumnDialogViewModel _viewModel;

    public ColumnDialog(ColumnDialogViewModel viewModel)
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
        if (string.IsNullOrWhiteSpace(_viewModel.Name))
        {
            NameInput.Focus();
            return false;
        }
        return _viewModel.TryGetLimit(out _);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (Validate()) DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
