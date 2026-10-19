using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenCalendar.UI.ViewModels;

namespace OpenCalendar.UI.Views;

public partial class EventEditorWindow : Window
{
    private readonly EventEditorViewModel _viewModel;

    public EventEditorWindow(EventEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        WindowStyling.ApplyRoundedCorners(this);

        viewModel.CloseRequested += (_, _) => Close();

        // Commit time fields before saving with a keyboard shortcut (they bind on LostFocus).
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key is Key.S or Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                (Keyboard.FocusedElement as TextBox)?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        };

        Loaded += (_, _) =>
        {
            TitleInput.Focus();
            TitleInput.SelectAll();
        };
    }
}
