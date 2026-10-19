using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenCalendar.UI.ViewModels;

namespace OpenCalendar.UI.Controls;

/// <summary>Day / week time grid. Double-click on an empty slot creates an event there (rounded to 30 minutes).</summary>
public partial class WeekView : UserControl
{
    public static readonly double HourHeight = TimeGrid.HourHeight;
    public static readonly double HalfHourHeight = TimeGrid.HourHeight / 2;
    public static readonly double GridHeight = TimeGrid.TotalHeight;

    private MainViewModel? _viewModel;
    private bool _scrolledInitially;

    public WeekView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach(DataContext as MainViewModel);
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible) ScrollToWorkday(force: false);
        };
    }

    private void Attach(MainViewModel? viewModel)
    {
        if (_viewModel is not null) _viewModel.ViewRebuilt -= OnViewRebuilt;
        _viewModel = viewModel;
        if (_viewModel is not null) _viewModel.ViewRebuilt += OnViewRebuilt;
    }

    private void OnViewRebuilt(object? sender, EventArgs e) => ScrollToWorkday(force: false);

    /// <summary>On first display, start at the beginning of the working day instead of midnight.</summary>
    private void ScrollToWorkday(bool force)
    {
        if (_viewModel is null || (_scrolledInitially && !force) || !IsVisible) return;
        _scrolledInitially = true;
        Dispatcher.InvokeAsync(() => BodyScroll.ScrollToVerticalOffset(_viewModel.InitialScrollOffset),
            System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnColumnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || _viewModel is null) return;
        if (sender is not FrameworkElement { DataContext: DayColumnViewModel column } element) return;

        var minutes = e.GetPosition(element).Y / TimeGrid.PixelsPerMinute;
        var rounded = Math.Clamp(Math.Floor(minutes / 30) * 30, 0, 23 * 60 + 30);
        _viewModel.CreateEventAt(column.Date.ToDateTime(TimeOnly.MinValue).AddMinutes(rounded), allDay: false);
        e.Handled = true;
    }
}
