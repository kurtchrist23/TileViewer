using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TileViewer.Models;

public class TileCache
{
    private readonly int _capacity;
    private readonly LinkedList<(Key Key, BitmapSource Image)> _lru = new();
    private readonly Dictionary<Key, LinkedListNode<(Key Key, BitmapSource Image)>> _index = new();
    private readonly HashSet<Key> _miss = new();
    private BitmapSource? _blank;

    public TileCache(int capacity = 800)
    {
        _capacity = capacity;
    }

    public record struct Key(int TileSetId, int Z, int Tx, int Ty);

    public BitmapSource Get(TileSet ts, int z, int tx, int ty)
    {
        var key = new Key(ts.GetHashCode(), z, tx, ty);
        if (_index.TryGetValue(key, out var node))
        {
            _lru.Remove(node);
            _lru.AddLast(node);
            return node.Value.Image;
        }
        if (_miss.Contains(key)) return Blank;

        var p = ts.TilePath(z, tx, ty);
        if (File.Exists(p))
        {
            try
            {
                var bmp = LoadBitmap(p);
                var added = _lru.AddLast((key, bmp));
                _index[key] = added;
                while (_lru.Count > _capacity)
                {
                    var first = _lru.First!;
                    _index.Remove(first.Value.Key);
                    _lru.RemoveFirst();
                }
                return bmp;
            }
            catch { }
        }
        _miss.Add(key);
        return Blank;
    }

    private static BitmapSource LoadBitmap(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = stream;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    public BitmapSource Blank
    {
        get
        {
            if (_blank == null)
            {
                const int d = TileSet.DT;
                var drawing = new DrawingVisual();
                using (var ctx = drawing.RenderOpen())
                {
                    var bg = new SolidColorBrush(Color.FromRgb(20, 20, 35));
                    bg.Freeze();
                    ctx.DrawRectangle(bg, null, new System.Windows.Rect(0, 0, d, d));
                    var pen = new System.Windows.Media.Pen(
                        new SolidColorBrush(Color.FromRgb(40, 40, 60)), 1);
                    pen.Freeze();
                    ctx.DrawRectangle(null, pen, new System.Windows.Rect(0.5, 0.5, d - 1, d - 1));
                }
                var rtb = new RenderTargetBitmap(d, d, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(drawing);
                rtb.Freeze();
                _blank = rtb;
            }
            return _blank;
        }
    }

    public void Flush()
    {
        _lru.Clear();
        _index.Clear();
        _miss.Clear();
    }
}
