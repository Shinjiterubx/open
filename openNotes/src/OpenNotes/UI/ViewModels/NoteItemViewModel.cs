using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenNotes.Core.Markdown;
using OpenNotes.Core.Models;
using OpenNotes.Core.Reminders;

namespace OpenNotes.UI.ViewModels;

/// <summary>A note as shown in the sidebar list.</summary>
public sealed class NoteItemViewModel : ObservableObject
{
    private static readonly CultureInfo Hungarian = CultureInfo.GetCultureInfo("hu-HU");

    public NoteItemViewModel(Note model) => Model = model;

    public Note Model { get; }

    public Guid Id => Model.Id;
    public string Title => Model.DisplayTitle;
    public bool HasTitle => !string.IsNullOrWhiteSpace(Model.Title);
    public string Snippet => MarkdownText.Snippet(Model.Content, 140) is { Length: > 0 } text ? text : "Üres jegyzet";
    public DateTimeOffset UpdatedAt => Model.UpdatedAt;
    public string UpdatedText => FormatRelative(Model.UpdatedAt);

    public NotePriority Priority => Model.Priority;
    public bool HasPriority => Model.Priority != NotePriority.None;
    public bool HasDue => Model.DueDate is not null;
    public bool IsDone => Model.IsDone;
    public bool HasReminder => ReminderRules.IsActive(Model);
    public string DueText => DueDateText.Relative(Model, DateTime.Now);

    /// <summary>Overdue / Today / Upcoming for active reminders, otherwise <c>null</c> (shown neutral).</summary>
    public ReminderState? DueState =>
        Model.DueDate is null || Model.IsDone ? null : ReminderRules.Classify(ReminderRules.DueMoment(Model)!.Value, DateTime.Now);

    public bool IsPinned
    {
        get => Model.IsPinned;
        set
        {
            if (Model.IsPinned == value) return;
            Model.IsPinned = value;
            OnPropertyChanged();
        }
    }

    public bool Matches(string query) =>
        string.IsNullOrWhiteSpace(query)
        || Model.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || Model.Content.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Call after the underlying note changed.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(HasTitle));
        OnPropertyChanged(nameof(Snippet));
        OnPropertyChanged(nameof(UpdatedAt));
        OnPropertyChanged(nameof(UpdatedText));
        RefreshTaskState();
    }

    /// <summary>Priority / due date / reminder changed, or time moved on.</summary>
    public void RefreshTaskState()
    {
        OnPropertyChanged(nameof(Priority));
        OnPropertyChanged(nameof(HasPriority));
        OnPropertyChanged(nameof(HasDue));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(HasReminder));
        OnPropertyChanged(nameof(DueText));
        OnPropertyChanged(nameof(DueState));
    }

    internal static string FormatRelative(DateTimeOffset timestamp)
    {
        var local = timestamp.LocalDateTime;
        var today = DateTime.Today;

        if (local.Date == today) return "ma " + local.ToString("HH:mm", Hungarian);
        if (local.Date == today.AddDays(-1)) return "tegnap " + local.ToString("HH:mm", Hungarian);
        if (local.Year == today.Year) return local.ToString("MMM d.", Hungarian);
        return local.ToString("yyyy. MM. dd.", Hungarian);
    }
}
