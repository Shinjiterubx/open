using CommunityToolkit.Mvvm.ComponentModel;
using OpenCalendar.Core.Models;
using OpenCalendar.Services;

namespace OpenCalendar.UI.ViewModels;

/// <summary>A calendar in the sidebar list (visibility toggle, colour, context menu actions).</summary>
public sealed class CalendarListItemViewModel : ObservableObject
{
    private readonly CalendarService _service;

    public CalendarListItemViewModel(CalendarDocument document, CalendarService service)
    {
        Document = document;
        _service = service;
        Name = document.Name;
        Color = document.Color;
    }

    public CalendarDocument Document { get; }
    public Guid Id => Document.Id;
    /// <summary>Snapshot at creation; the list is rebuilt when the calendar is renamed or recoloured.</summary>
    public string Name { get; }
    public string Color { get; }
    public int EventCount => Document.Events.Count;

    public bool IsVisible
    {
        get => Document.IsVisible;
        set
        {
            if (Document.IsVisible == value) return;
            _service.SetVisible(Document, value);
            OnPropertyChanged();
        }
    }
}
