using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OpenKanban.UI.ViewModels;
using OpenKanban.UI.Views;

namespace OpenKanban.UI.Services;

/// <summary>Creates windows and shows dialogs owned by the active window.</summary>
public sealed class WindowService : IWindowService
{
    private readonly IServiceProvider _services;

    public WindowService(IServiceProvider services) => _services = services;

    private MainWindow Main => _services.GetRequiredService<MainWindow>();

    public void ShowMain() => WindowStyling.BringToFront(Main);

    public void ShowCardEditor(CardEditorViewModel editor) => ShowDialog(new CardEditorWindow(editor));

    public bool ShowWorkspaceDialog(WorkspaceDialogViewModel dialog) => ShowDialog(new WorkspaceDialog(dialog));

    public bool ShowBoardDialog(BoardDialogViewModel dialog) => ShowDialog(new BoardDialog(dialog));

    public bool ShowColumnDialog(ColumnDialogViewModel dialog) => ShowDialog(new ColumnDialog(dialog));

    public void ShowSettings() => ShowDialog(_services.GetRequiredService<SettingsWindow>());

    private bool ShowDialog(Window window)
    {
        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w != window);
        if (owner is null && Main.IsVisible) owner = Main;

        if (owner is not null) window.Owner = owner;
        else window.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        return window.ShowDialog() == true;
    }
}
