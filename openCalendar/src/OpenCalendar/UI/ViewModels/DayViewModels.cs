using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OpenCalendar.UI.ViewModels;

internal static class Hu
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("hu-HU");

    public static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], Culture) + text[1..];

    public static readonly string[] ShortDayNames = { "H", "K", "Sze", "Cs", "P", "Szo", "V" };
}

/// <summary>A cell of the month grid.</summary>
public sealed class MonthCellViewModel
{
    public const int MaxVisibleEntries = 3;

    public MonthCellViewModel(DateOnly date, bool isCurrentMonth, bool isToday, bool isSelected, IReadOnlyList<EntryViewModel> entries)
    {
        Date = date;
        IsCurrentMonth = isCurrentMonth;
        IsToday = isToday;
        IsSelected = isSelected;
        Entries = entries.Take(MaxVisibleEntries).ToList();
        MoreCount = Math.Max(0, entries.Count - MaxVisibleEntries);
    }

    public DateOnly Date { get; }
    public string DayNumber => Date.Day == 1 ? Date.ToString("MMM d.", Hu.Culture) : Date.Day.ToString(Hu.Culture);
    public bool IsCurrentMonth { get; }
    public bool IsToday { get; }
    public bool IsSelected { get; }
    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    public IReadOnlyList<EntryViewModel> Entries { get; }
    public int MoreCount { get; }
    public bool HasMore => MoreCount > 0;
    public string MoreText => $"+{MoreCount} további";
}

/// <summary>A day column of the week / day view.</summary>
public sealed partial class DayColumnViewModel : ObservableObject
{
    [ObservableProperty] private double _nowMinute;

    public DayColumnViewModel(DateOnly date, bool isToday, IReadOnlyList<EntryViewModel> allDay, IReadOnlyList<EntryViewModel> timed)
    {
        Date = date;
        IsToday = isToday;
        AllDay = allDay;
        Timed = timed;
    }

    public DateOnly Date { get; }
    public bool IsToday { get; }
    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    public string DayName => Hu.Capitalize(Date.ToString("ddd", Hu.Culture).TrimEnd('.'));
    public string DayNumber => Date.Day.ToString(Hu.Culture);
    public IReadOnlyList<EntryViewModel> AllDay { get; }
    public IReadOnlyList<EntryViewModel> Timed { get; }
}

/// <summary>A day group in the agenda (list) view.</summary>
public sealed class AgendaDayViewModel
{
    public AgendaDayViewModel(DateOnly date, DateOnly today, IReadOnlyList<EntryViewModel> entries)
    {
        Date = date;
        IsToday = date == today;
        Entries = entries;
        var relative = date == today ? "Ma" : date == today.AddDays(1) ? "Holnap" : date == today.AddDays(-1) ? "Tegnap" : null;
        var full = Hu.Capitalize(date.ToString("MMMM d., dddd", Hu.Culture));
        Header = relative is null ? full : $"{relative} · {full}";
    }

    public DateOnly Date { get; }
    public bool IsToday { get; }
    public string Header { get; }
    public IReadOnlyList<EntryViewModel> Entries { get; }
}

/// <summary>A day in the small sidebar calendar.</summary>
public sealed class MiniDayViewModel
{
    public MiniDayViewModel(DateOnly date, bool isCurrentMonth, bool isToday, bool isSelected, bool hasEntries)
    {
        Date = date;
        IsCurrentMonth = isCurrentMonth;
        IsToday = isToday;
        IsSelected = isSelected;
        HasEntries = hasEntries;
    }

    public DateOnly Date { get; }
    public string Day => Date.Day.ToString(Hu.Culture);
    public bool IsCurrentMonth { get; }
    public bool IsToday { get; }
    public bool IsSelected { get; }
    public bool HasEntries { get; }
}

/// <summary>Small month calendar in the sidebar; browsing months doesn't change the main view until a day is picked.</summary>
public sealed partial class MiniCalendarViewModel : ObservableObject
{
    private Func<DateOnly, DateOnly, ISet<DateOnly>> _datesWithEntries = (_, _) => new HashSet<DateOnly>();
    private DateOnly _selected = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty] private DateOnly _displayMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public ObservableCollection<MiniDayViewModel> Days { get; } = new();

    public string Title => Hu.Capitalize(DisplayMonth.ToString("yyyy. MMMM", Hu.Culture));

    public event EventHandler<DateOnly>? DateSelected;

    public void Update(DateOnly selected, Func<DateOnly, DateOnly, ISet<DateOnly>> datesWithEntries)
    {
        _selected = selected;
        _datesWithEntries = datesWithEntries;
        var month = new DateOnly(selected.Year, selected.Month, 1);
        if (month != DisplayMonth) DisplayMonth = month;
        else Rebuild();
    }

    partial void OnDisplayMonthChanged(DateOnly value) => Rebuild();

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void PreviousMonth() => DisplayMonth = DisplayMonth.AddMonths(-1);

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void NextMonth() => DisplayMonth = DisplayMonth.AddMonths(1);

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Select(MiniDayViewModel? day)
    {
        if (day is not null) DateSelected?.Invoke(this, day.Date);
    }

    private void Rebuild()
    {
        var start = Core.Time.WeekMath.MonthGridStart(DisplayMonth.Year, DisplayMonth.Month);
        var busy = _datesWithEntries(start, start.AddDays(42));
        var today = DateOnly.FromDateTime(DateTime.Today);

        Days.Clear();
        for (var i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            Days.Add(new MiniDayViewModel(date, date.Month == DisplayMonth.Month, date == today, date == _selected, busy.Contains(date)));
        }
        OnPropertyChanged(nameof(Title));
    }
}
