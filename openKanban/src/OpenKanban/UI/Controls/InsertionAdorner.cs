using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace OpenKanban.UI.Controls;

/// <summary>Accent line showing where a dragged card / column will land.</summary>
public sealed class InsertionAdorner : Adorner
{
    private readonly Pen _pen;
    private readonly bool _vertical;
    private double _offset;

    public InsertionAdorner(UIElement adornedElement, bool vertical, Brush brush) : base(adornedElement)
    {
        _vertical = vertical;
        _pen = new Pen(brush, 3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        _pen.Freeze();
        IsHitTestVisible = false;
    }

    /// <summary>Y (horizontal line) or X (vertical line) in the adorned element's coordinates.</summary>
    public double Offset
    {
        get => _offset;
        set
        {
            if (Math.Abs(_offset - value) < 0.5) return;
            _offset = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        var size = AdornedElement.RenderSize;
        if (_vertical) drawingContext.DrawLine(_pen, new Point(_offset, 4), new Point(_offset, Math.Max(4, size.Height - 4)));
        else drawingContext.DrawLine(_pen, new Point(6, _offset), new Point(Math.Max(6, size.Width - 6), _offset));
    }
}
