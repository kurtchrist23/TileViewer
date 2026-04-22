using System.Windows;
using System.Windows.Media;

namespace TileViewer.Views;

public class TileHost : FrameworkElement
{
    public Action<DrawingContext, Size>? RenderTiles;

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        RenderTiles?.Invoke(dc, RenderSize);
    }
}
