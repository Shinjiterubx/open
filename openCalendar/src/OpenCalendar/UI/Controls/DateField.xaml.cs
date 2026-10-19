using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using OpenCalendar.UI.ViewModels;

namespace OpenCalendar.UI.Controls;

/// <summary>Compact date input: shows "2026. okt. 13., kedd"; a click opens a calendar popup.</summary>
public partial class DateField : UserControl
{
    public static readonly DependencyProperty SelectedDateProperty = DependencyProperty.Register(
        nameof(SelectedDate), typeof(DateTime?), typeof(DateField),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((DateField)d).UpdateText()));

    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
        nameof(Placeholder), typeof(string), typeof(DateField), new PropertyMetadata("Válassz dátumot", (d, _) => ((DateField)d).UpdateText()));

    private bool _syncing;

    public DateField()
    {
        InitializeComponent();
        Picker.SelectedDatesChanged += OnPickerChanged;
        Picker.PreviewMouseUp += (_, _) =>
        {
            // A Calendar inside a Popup keeps mouse capture after a click – release it.
            if (Mouse.Captured is CalendarItem or CalendarDayButton) Mouse.Capture(null);
        };
        Popup.Opened += (_, _) =>
        {
            _syncing = true;
            Picker.SelectedDate = SelectedDate;
            Picker.DisplayDate = SelectedDate ?? DateTime.Today;
            _syncing = false;
        };
        UpdateText();
    }

    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    private void OnPickerChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || Picker.SelectedDate is not { } date) return;
        SetCurrentValue(SelectedDateProperty, date.Date);
        Toggle.IsChecked = false;
    }

    private void OnTodayClick(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(SelectedDateProperty, DateTime.Today);
        Toggle.IsChecked = false;
    }

    private void UpdateText()
    {
        if (DateText is null) return;
        if (SelectedDate is { } date)
        {
            DateText.Text = Hu.Capitalize(date.ToString("yyyy. MMM d., dddd", Hu.Culture));
            DateText.Foreground = (System.Windows.Media.Brush)FindResource("InkBrush");
        }
        else
        {
            DateText.Text = Placeholder;
            DateText.Foreground = (System.Windows.Media.Brush)FindResource("InkMutedBrush");
        }
    }
}
