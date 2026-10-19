using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenCalendar.UI.ViewModels;

namespace OpenCalendar.UI.Controls;

/// <summary>Month grid. Click: select day · double-click: new event on that day.</summary>
public partial class MonthView : UserControl
{
    public MonthView() => InitializeComponent();

    private void OnCellMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: MonthCellViewModel cell } || DataContext is not MainViewModel main) return;

        if (e.ClickCount == 2)
        {
            main.CreateEventAt(cell.Date.ToDateTime(new TimeOnly(9, 0)), allDay: false);
            e.Handled = true;
        }
        else if (main.SelectedDate != cell.Date)
        {
            main.SelectedDate = cell.Date;
        }
    }
}
