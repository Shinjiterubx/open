using OpenCalendar.UI.ViewModels;

namespace OpenCalendar.UI.Services;

public interface IWindowService
{
    void ShowMain();

    /// <returns><c>true</c> if the editor closed with save / delete (see <see cref="EventEditorViewModel.Outcome"/>).</returns>
    bool ShowEventEditor(EventEditorViewModel editor);

    bool ShowCalendarDialog(CalendarDialogViewModel dialog);

    void ShowSettings();
}
