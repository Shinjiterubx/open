using OpenKanban.UI.ViewModels;

namespace OpenKanban.UI.Services;

public interface IWindowService
{
    void ShowMain();

    void ShowCardEditor(CardEditorViewModel editor);

    bool ShowWorkspaceDialog(WorkspaceDialogViewModel dialog);

    bool ShowBoardDialog(BoardDialogViewModel dialog);

    bool ShowColumnDialog(ColumnDialogViewModel dialog);

    void ShowSettings();
}
