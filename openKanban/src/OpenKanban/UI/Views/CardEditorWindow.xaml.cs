using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenKanban.UI.ViewModels;

namespace OpenKanban.UI.Views;

public partial class CardEditorWindow : Window
{
    private readonly CardEditorViewModel _viewModel;

    public CardEditorWindow(CardEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        WindowStyling.ApplyRoundedCorners(this);

        viewModel.CloseRequested += (_, _) => Close();
        PreviewKeyDown += OnPreviewKeyDown;
        NewItemInput.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            _viewModel.AddChecklistItemCommand.Execute(null);
            e.Handled = true;
        };

        Loaded += (_, _) =>
        {
            TitleInput.Focus();
            TitleInput.CaretIndex = TitleInput.Text.Length;
        };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _viewModel.CancelCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            // Time box binds on LostFocus – commit it first.
            (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            _viewModel.SaveCommand.Execute(null);
            e.Handled = true;
        }
    }
}
