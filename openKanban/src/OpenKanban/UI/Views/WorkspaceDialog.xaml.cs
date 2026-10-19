using System.Windows;
using OpenKanban.UI.ViewModels;

namespace OpenKanban.UI.Views;

public partial class WorkspaceDialog : Window
{
    private readonly WorkspaceDialogViewModel _viewModel;

    public WorkspaceDialog(WorkspaceDialogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        DialogShell.Attach(this, Surface, Validate);
        Loaded += (_, _) => NameInput.Focus();
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
