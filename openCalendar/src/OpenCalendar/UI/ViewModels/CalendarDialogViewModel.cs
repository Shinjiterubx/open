using CommunityToolkit.Mvvm.ComponentModel;
using OpenCalendar.Core.Models;

namespace OpenCalendar.UI.ViewModels;

/// <summary>Name + colour of a calendar (create / edit).</summary>
public sealed partial class CalendarDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _name;
    [ObservableProperty] private string _color;

    public CalendarDialogViewModel(string title, string name, string color)
    {
        Title = title;
        _name = name;
        _color = color;
    }

    public string Title { get; }
    public IReadOnlyList<string> Palette => CalendarDocument.Palette;
}
