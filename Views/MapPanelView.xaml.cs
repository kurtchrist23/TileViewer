using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using TileViewer.Models;

namespace TileViewer.Views;

public partial class MapPanelView : UserControl
{
    public MainWindow? Host { get; set; }
    public TileSet? Tileset { get; private set; }
    public double Lat { get; set; } = 0;
    public double Lon { get; set; } = 0;
    public int Zoom { get; set; } = 3;
    public string DriveLabelText { get; private set; } = "";

    private Point? _dragStart;
    private bool _comboSync;

    public MapPanelView()
    {
        InitializeComponent();
        MapSurface.RenderTiles = DrawTiles;
    }

    public void RefreshCombo()
    {
        if (Host == null) return;
        _comboSync = true;
        TilesetCombo.Items.Clear();
        TilesetCombo.Items.Add("(none)");
        foreach (var ts in Host.Tilesets) TilesetCombo.Items.Add(ts.Display);
        if (Tileset != null)
        {
            var idx = Host.Tilesets.IndexOf(Tileset);
            TilesetCombo.SelectedIndex = idx >= 0 ? idx + 1 : 0;
        }
        else TilesetCombo.SelectedIndex = 0;
        _comboSync = false;
    }

    public void Clear()
    {
        Tileset = null;
        DriveLabelText = "";
        DriveLabel.Text = "";
        DriveOverlay.Text = "";
        InfoLabel.Text = "(empty)";
        InfoLabel.Foreground = (System.Windows.Media.Brush)FindResource("Overlay0Brush");
        RefreshCombo();
        ScheduleRender();
    }

    public void Assign(TileSet ts)
    {
        Tileset = ts;
        InfoLabel.Text = $"z{ts.MinZoom}-{ts.MaxZoom}  {ts.TileSize}px";
        InfoLabel.Foreground = (System.Windows.Media.Brush)FindResource("SapphireBrush");
        RefreshCombo();
        if (ts.Bbox.HasValue)
        {
            var (lat, lon) = ts.Center();
            Lat = lat; Lon = lon;
            var w = MapSurface.ActualWidth > 0 ? MapSurface.ActualWidth : 600;
            var h = MapSurface.ActualHeight > 0 ? MapSurface.ActualHeight : 400;
            Zoom = ts.FitZoom(w, h);
        }
        ScheduleRender();
    }

    public void SetDriveLabel(string label)
    {
        DriveLabelText = label;
        DriveLabel.Text = label;
        DriveOverlay.Text = label;
    }

    private void OpenBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select tileset folder" };
        if (dlg.ShowDialog() == true && Host != null)
            Host.LoadAndAssign(dlg.FolderName, this);
    }

    private void DeepBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select site folder (searches inside)" };
        if (dlg.ShowDialog() != true || Host == null) return;
        var root = dlg.FolderName;
        if (File.Exists(Path.Combine(root, "tilemapresource.xml")))
        {
            Host.LoadAndAssign(root, this);
            return;
        }
        try
        {
            foreach (var dir in EnumerateExcluding(root, "RawData"))
            {
                if (File.Exists(Path.Combine(dir, "tilemapresource.xml")))
                {
                    Host.LoadAndAssign(dir, this);
                    return;
                }
            }
        }
        catch { }
        MessageBox.Show($"No tilemapresource.xml under:\n{root}", "Not found",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static IEnumerable<string> EnumerateExcluding(string root, string exclude)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            yield return d;
            string[] subs;
            try { subs = Directory.GetDirectories(d); } catch { continue; }
            foreach (var s in subs)
                if (!string.Equals(Path.GetFileName(s), exclude, StringComparison.OrdinalIgnoreCase))
                    stack.Push(s);
        }
    }

    private void TilesetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_comboSync || Host == null) return;
        var idx = TilesetCombo.SelectedIndex - 1;
        if (idx >= 0 && idx < Host.Tilesets.Count) Assign(Host.Tilesets[idx]);
        else
        {
            Tileset = null;
            InfoLabel.Text = "(empty)";
            InfoLabel.Foreground = (System.Windows.Media.Brush)FindResource("Overlay0Brush");
            ScheduleRender();
        }
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Host?.RemovePanel(this);
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(MapSurface);
        MapSurface.CaptureMouse();
        Focus();
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndDrag();
    private void Canvas_MouseLeave(object sender, MouseEventArgs e) => EndDrag();

    private void EndDrag()
    {
        _dragStart = null;
        MapSurface.ReleaseMouseCapture();
    }

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart == null || Tileset == null) return;
        var p = e.GetPosition(MapSurface);
        var dx = p.X - _dragStart.Value.X;
        var dy = p.Y - _dragStart.Value.Y;
        _dragStart = p;
        var ts = Tileset;
        var z = Zoom;
        var cx = ts.LonToTx(Lon, z) - dx / TileSet.DT;
        var cy = ts.LatToTy(Lat, z) + dy / TileSet.DT;
        Lon = ts.TxToLon(cx, z);
        Lat = Math.Max(-85, Math.Min(85, ts.TyToLat(cy, z)));
        ScheduleRender();
        Host?.SyncOthers(this);
    }

    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        ZDelta(e.Delta > 0 ? 1 : -1);
    }

    public void ZDelta(int d)
    {
        var ts = Tileset;
        var minZ = ts?.MinZoom ?? 0;
        var maxZ = ts?.MaxZoom ?? 22;
        var nz = Math.Max(minZ, Math.Min(maxZ, Zoom + d));
        if (nz != Zoom)
        {
            Zoom = nz;
            ScheduleRender();
            Host?.SyncOthers(this);
        }
    }

    private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleRender();

    public void ScheduleRender()
    {
        var ts = Tileset;
        if (ts == null)
        {
            EmptyText.Visibility = Visibility.Visible;
            ZoomOverlay.Text = "";
            NameOverlay.Text = "";
            CoordOverlay.Text = "";
        }
        else
        {
            EmptyText.Visibility = Visibility.Collapsed;
            ZoomOverlay.Text = $"z{Zoom}";
            NameOverlay.Text = ts.Name;
            CoordOverlay.Text = $"{Lat:F4}, {Lon:F4}";
        }
        MapSurface.InvalidateVisual();
    }

    private void DrawTiles(DrawingContext dc, Size size)
    {
        var w = size.Width;
        var h = size.Height;
        if (w < 2 || h < 2) return;
        var ts = Tileset;
        if (ts == null) return;
        var cache = Host?.Cache;
        if (cache == null) return;

        var z = Zoom;
        var cx = ts.LonToTx(Lon, z);
        var cy = ts.LatToTy(Lat, z);
        var nx = ts.Nx(z);
        var ny = ts.Ny(z);

        const int DT = TileSet.DT;
        var hw = w / DT / 2 + 1;
        var hh = h / DT / 2 + 1;
        var tx0 = (int)Math.Floor(cx - hw);
        var tx1 = (int)Math.Ceiling(cx + hw);
        var ty0 = (int)Math.Floor(cy - hh);
        var ty1 = (int)Math.Ceiling(cy + hh);

        for (int tx = tx0; tx <= tx1; tx++)
        {
            for (int ty = ty0; ty <= ty1; ty++)
            {
                if (ty < 0 || ty >= ny) continue;
                var atx = tx % nx;
                if (atx < 0) atx += nx;
                var sx = (int)(w / 2 + (tx - cx) * DT);
                var sy = (int)(h / 2 - (ty - cy) * DT - DT);
                var img = cache.Get(ts, z, atx, ty);
                if (img != null)
                    dc.DrawImage(img, new Rect(sx, sy, DT, DT));
            }
        }
    }
}
