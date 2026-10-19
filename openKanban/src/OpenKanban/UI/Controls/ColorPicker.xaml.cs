using System.Windows;
using System.Windows.Controls;

namespace OpenKanban.UI.Controls;

/// <summary>Row of colour swatches bound to a "#RRGGBB" string.</summary>
public partial class ColorPicker : UserControl
{
    public static readonly DependencyProperty ColorsProperty = DependencyProperty.Register(
        nameof(Colors), typeof(IEnumerable<string>), typeof(ColorPicker), new PropertyMetadata(null, (d, e) => ((ColorPicker)d).Swatches.ItemsSource = e.NewValue as IEnumerable<string>));

    public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
        nameof(SelectedColor), typeof(string), typeof(ColorPicker),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((ColorPicker)d).SyncChecks()));

    public static readonly DependencyProperty SwatchSizeProperty = DependencyProperty.Register(
        nameof(SwatchSize), typeof(double), typeof(ColorPicker), new PropertyMetadata(30d));

    private bool _syncing;

    public ColorPicker()
    {
        InitializeComponent();
        GroupName = "swatch-" + Guid.NewGuid().ToString("N");
    }

    /// <summary>Unique radio group per picker instance (several pickers can live in one window).</summary>
    public string GroupName { get; }

    public IEnumerable<string>? Colors
    {
        get => (IEnumerable<string>?)GetValue(ColorsProperty);
        set => SetValue(ColorsProperty, value);
    }

    public string? SelectedColor
    {
        get => (string?)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public double SwatchSize
    {
        get => (double)GetValue(SwatchSizeProperty);
        set => SetValue(SwatchSizeProperty, value);
    }

    private void OnSwatchLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string color } swatch)
        {
            _syncing = true;
            swatch.IsChecked = string.Equals(color, SelectedColor, StringComparison.OrdinalIgnoreCase);
            _syncing = false;
        }
    }

    private void OnSwatchChecked(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not RadioButton { Tag: string color }) return;
        SetCurrentValue(SelectedColorProperty, color);
    }

    /// <summary>External change (e.g. "no cover"): update the check marks.</summary>
    private void SyncChecks()
    {
        if (Swatches is null) return;
        _syncing = true;
        foreach (var item in Swatches.Items)
        {
            if (Swatches.ItemContainerGenerator.ContainerFromItem(item) is ContentPresenter presenter
                && VisualTreeHelperFirst<RadioButton>(presenter) is { } radio)
                radio.IsChecked = string.Equals(item as string, SelectedColor, StringComparison.OrdinalIgnoreCase);
        }
        _syncing = false;
    }

    private static T? VisualTreeHelperFirst<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            if (VisualTreeHelperFirst<T>(child) is { } nested) return nested;
        }
        return null;
    }
}
