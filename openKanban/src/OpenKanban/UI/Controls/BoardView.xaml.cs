using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using OpenKanban.UI.ViewModels;

namespace OpenKanban.UI.Controls;

/// <summary>
/// The open board. Handles everything that is purely interaction: card / column drag &amp; drop with an
/// insertion marker, click-to-open, the inline "add card" composer keys and edge auto-scroll.
/// All data changes go through <see cref="MainViewModel"/>.
/// </summary>
public partial class BoardView : UserControl
{
    private const string CardFormat = "openKanban.card";
    private const string ColumnFormat = "openKanban.column";
    private const double AutoScrollEdge = 48;
    private const double AutoScrollStep = 18;

    private Point _pressPoint;
    private CardViewModel? _pressedCard;
    private ColumnViewModel? _pressedColumn;
    private bool _dragging;
    private InsertionAdorner? _adorner;
    private UIElement? _adornedElement;

    public BoardView() => InitializeComponent();

    private MainViewModel? Main => DataContext as MainViewModel;

    // ───────────────────────── cards: click & drag ─────────────────────────

    private void OnCardMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject, (DependencyObject)sender)) return;
        if (sender is not FrameworkElement { DataContext: CardViewModel card }) return;

        _pressPoint = e.GetPosition(this);
        _pressedCard = card;
        _dragging = false;
    }

    private void OnCardMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressedCard is null || e.LeftButton != MouseButtonState.Pressed || _dragging) return;
        if (!IsDragGesture(e.GetPosition(this))) return;

        _dragging = true;
        var card = _pressedCard;
        _pressedCard = null;
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(CardFormat, card), DragDropEffects.Move);
        }
        finally
        {
            RemoveAdorner();
            _dragging = false;
        }
    }

    private void OnCardMouseUp(object sender, MouseButtonEventArgs e)
    {
        var card = _pressedCard;
        _pressedCard = null;
        if (card is null || _dragging) return;
        if (sender is FrameworkElement { DataContext: CardViewModel clicked } && clicked == card)
        {
            Main?.OpenCardCommand.Execute(card);
            e.Handled = true;
        }
    }

    // ───────────────────────── cards: drop on a column ─────────────────────────

    private void OnColumnDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(CardFormat)) return;   // let column drags bubble to the board
        if (sender is not FrameworkElement { DataContext: ColumnViewModel column } element) return;

        var host = FindDescendant<ItemsControl>(element, "CardsHost");
        if (host is null) return;

        var source = e.Data.GetData(CardFormat) as CardViewModel;
        var allowed = column.Cards.Any(c => c.Id == source?.Id) || !column.IsFull;
        e.Effects = allowed ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;

        if (!allowed)
        {
            RemoveAdorner();
            return;
        }

        var (_, offset) = CardInsertion(host, e.GetPosition(host));
        ShowAdorner(host, vertical: false, offset);
    }

    private void OnColumnDragLeave(object sender, DragEventArgs e) => RemoveAdorner();

    private void OnColumnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(CardFormat)) return;
        e.Handled = true;
        RemoveAdorner();

        if (sender is not FrameworkElement { DataContext: ColumnViewModel column } element) return;
        if (e.Data.GetData(CardFormat) is not CardViewModel card || Main is null) return;
        if (FindDescendant<ItemsControl>(element, "CardsHost") is not { } host) return;

        var (visibleIndex, _) = CardInsertion(host, e.GetPosition(host));
        Main.MoveCard(card.Id, column.Id, ToModelIndex(column, visibleIndex));
    }

    /// <summary>Visible insertion index and the Y of the marker line, from the mouse position over the cards list.</summary>
    private static (int Index, double Offset) CardInsertion(ItemsControl host, Point position)
    {
        var count = host.Items.Count;
        for (var i = 0; i < count; i++)
        {
            if (host.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container) continue;
            var top = container.TranslatePoint(new Point(0, 0), host).Y;
            var height = container.ActualHeight;
            if (position.Y < top + height / 2) return (i, Math.Max(2, top - 4));
        }

        if (count > 0 && host.ItemContainerGenerator.ContainerFromIndex(count - 1) is FrameworkElement last)
            return (count, last.TranslatePoint(new Point(0, 0), host).Y + last.ActualHeight - 4);
        return (0, 4);
    }

    /// <summary>The cards list may be filtered: map the visible index to the column's real card index.</summary>
    private static int ToModelIndex(ColumnViewModel column, int visibleIndex)
    {
        var modelCards = column.Model.Cards;
        if (visibleIndex < column.Cards.Count) return modelCards.IndexOf(column.Cards[visibleIndex].Model);
        return column.Cards.Count == 0 ? modelCards.Count : modelCards.IndexOf(column.Cards[^1].Model) + 1;
    }

    // ───────────────────────── columns: drag & drop ─────────────────────────

    private void OnColumnHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideButton(e.OriginalSource as DependencyObject, (DependencyObject)sender)) return;
        if (sender is not FrameworkElement { DataContext: ColumnViewModel column }) return;
        _pressPoint = e.GetPosition(this);
        _pressedColumn = column;
    }

    private void OnColumnHeaderMouseMove(object sender, MouseEventArgs e)
    {
        if (_pressedColumn is null || e.LeftButton != MouseButtonState.Pressed) return;
        if (!IsDragGesture(e.GetPosition(this))) return;

        var column = _pressedColumn;
        _pressedColumn = null;
        try
        {
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(ColumnFormat, column), DragDropEffects.Move);
        }
        finally
        {
            RemoveAdorner();
        }
    }

    private void OnColumnsDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(ColumnFormat)) return;
        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        var (_, offset) = ColumnInsertion(e.GetPosition(ColumnsHost));
        ShowAdorner(ColumnsHost, vertical: true, offset);
    }

    private void OnColumnsDragLeave(object sender, DragEventArgs e) => RemoveAdorner();

    private void OnColumnsDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(ColumnFormat)) return;
        e.Handled = true;
        RemoveAdorner();

        if (e.Data.GetData(ColumnFormat) is not ColumnViewModel column || Main is null) return;
        var (index, _) = ColumnInsertion(e.GetPosition(ColumnsHost));
        Main.MoveColumn(column.Id, index);
    }

    private (int Index, double Offset) ColumnInsertion(Point position)
    {
        var count = ColumnsHost.Items.Count;
        for (var i = 0; i < count; i++)
        {
            if (ColumnsHost.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement container) continue;
            var left = container.TranslatePoint(new Point(0, 0), ColumnsHost).X;
            if (position.X < left + container.ActualWidth / 2) return (i, Math.Max(2, left - 6));
        }

        if (count > 0 && ColumnsHost.ItemContainerGenerator.ContainerFromIndex(count - 1) is FrameworkElement last)
            return (count, last.TranslatePoint(new Point(0, 0), ColumnsHost).X + last.ActualWidth - 6);
        return (0, 2);
    }

    // ───────────────────────── misc ─────────────────────────

    /// <summary>Scroll the board horizontally while dragging near its left / right edge.</summary>
    private void OnBoardPreviewDragOver(object sender, DragEventArgs e)
    {
        var x = e.GetPosition(BoardScroll).X;
        if (x < AutoScrollEdge) BoardScroll.ScrollToHorizontalOffset(BoardScroll.HorizontalOffset - AutoScrollStep);
        else if (x > BoardScroll.ActualWidth - AutoScrollEdge) BoardScroll.ScrollToHorizontalOffset(BoardScroll.HorizontalOffset + AutoScrollStep);
    }

    private void OnComposerKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ColumnViewModel column } || Main is null) return;

        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            Main.AddCardCommand.Execute(column);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Main.CloseComposerCommand.Execute(column);
            e.Handled = true;
        }
    }

    private void OnColumnMenuClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button) return;
        menu.DataContext = DataContext;          // commands live on MainViewModel
        menu.PlacementTarget = button;           // Tag = the column (command parameter)
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private bool IsDragGesture(Point current) =>
        Math.Abs(current.X - _pressPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
        Math.Abs(current.Y - _pressPoint.Y) > SystemParameters.MinimumVerticalDragDistance;

    private void ShowAdorner(UIElement element, bool vertical, double offset)
    {
        if (_adornedElement != element)
        {
            RemoveAdorner();
            var layer = AdornerLayer.GetAdornerLayer(element);
            if (layer is null) return;
            _adorner = new InsertionAdorner(element, vertical, (Brush)FindResource("AccentBrush"));
            layer.Add(_adorner);
            _adornedElement = element;
        }
        if (_adorner is not null) _adorner.Offset = offset;
    }

    private void RemoveAdorner()
    {
        if (_adorner is not null && _adornedElement is not null)
            AdornerLayer.GetAdornerLayer(_adornedElement)?.Remove(_adorner);
        _adorner = null;
        _adornedElement = null;
    }

    private static bool IsInsideButton(DependencyObject? source, DependencyObject stop)
    {
        for (var current = source; current is not null && current != stop; current = GetParent(current))
        {
            if (current is ButtonBase or TextBoxBase) return true;
        }
        return false;
    }

    private static DependencyObject? GetParent(DependencyObject child) =>
        child is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(child) : LogicalTreeHelper.GetParent(child);

    private static T? FindDescendant<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match && match.Name == name) return match;
            if (FindDescendant<T>(child, name) is { } nested) return nested;
        }
        return null;
    }
}
