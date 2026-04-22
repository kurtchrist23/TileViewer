using System.IO;
using System.Xml.Linq;

namespace TileViewer.Models;

public class TileSet
{
    public const int DT = 256;

    public string Path { get; }
    public string Name { get; }
    public int TileSize { get; private set; } = 256;
    public string Profile { get; private set; } = "mercator";
    public List<int> Zooms { get; } = new();
    public (double MinLat, double MinLon, double MaxLat, double MaxLon)? Bbox { get; private set; }

    private (double minx, double miny, double maxx, double maxy)? _rawBb;

    public TileSet(string path)
    {
        Path = path;
        Name = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
        Parse();
    }

    private void Parse()
    {
        var xmlPath = System.IO.Path.Combine(Path, "tilemapresource.xml");
        if (File.Exists(xmlPath))
        {
            try
            {
                var doc = XDocument.Load(xmlPath);
                var root = doc.Root;
                if (root != null)
                {
                    var tf = root.Element("TileFormat");
                    if (tf != null && int.TryParse(tf.Attribute("width")?.Value, out var w))
                        TileSize = w;
                    var tsElem = root.Element("TileSets");
                    if (tsElem != null)
                    {
                        var prof = (tsElem.Attribute("profile")?.Value ?? "").ToLowerInvariant();
                        if (prof.Contains("geodetic")) Profile = "geodetic";
                    }
                    var srs = root.Element("SRS")?.Value ?? "";
                    if (srs.Contains("4326")) Profile = "geodetic";
                    var bb = root.Element("BoundingBox");
                    if (bb != null)
                    {
                        _rawBb = (
                            ParseD(bb.Attribute("minx")),
                            ParseD(bb.Attribute("miny")),
                            ParseD(bb.Attribute("maxx")),
                            ParseD(bb.Attribute("maxy")));
                    }
                    foreach (var ts in root.Descendants("TileSet"))
                    {
                        var val = ts.Attribute("order")?.Value ?? ts.Attribute("href")?.Value ?? "0";
                        if (int.TryParse(val, out var z)) Zooms.Add(z);
                    }
                    Zooms.Sort();
                }
            }
            catch { }
        }

        if (Zooms.Count == 0 && Directory.Exists(Path))
        {
            foreach (var d in Directory.EnumerateDirectories(Path))
            {
                var n = System.IO.Path.GetFileName(d);
                if (int.TryParse(n, out var z)) Zooms.Add(z);
            }
            Zooms.Sort();
        }

        if (_rawBb.HasValue && Zooms.Count > 0)
            ResolveBbox();
    }

    private static double ParseD(XAttribute? a) =>
        a != null && double.TryParse(a.Value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

    private void ResolveBbox()
    {
        var v = _rawBb!.Value;
        var z = Zooms[Zooms.Count / 2];
        var zDir = System.IO.Path.Combine(Path, z.ToString());
        if (!Directory.Exists(zDir))
        {
            Bbox = (v.miny, v.minx, v.maxy, v.maxx);
            return;
        }

        var xDirs = new List<int>();
        try
        {
            foreach (var d in Directory.EnumerateDirectories(zDir))
            {
                var n = System.IO.Path.GetFileName(d);
                if (int.TryParse(n, out var x)) xDirs.Add(x);
            }
        }
        catch { }

        if (xDirs.Count == 0)
        {
            Bbox = (v.miny, v.minx, v.maxy, v.maxx);
            return;
        }

        var actualMin = xDirs.Min();
        var expA = (int)LonToTxRaw(v.miny, z);
        var expB = (int)LonToTxRaw(v.minx, z);
        if (Math.Abs(expA - actualMin) <= Math.Abs(expB - actualMin))
            Bbox = (v.minx, v.miny, v.maxx, v.maxy);
        else
            Bbox = (v.miny, v.minx, v.maxy, v.maxx);
    }

    private double LonToTxRaw(double lon, int z)
    {
        int nx;
        if (Profile == "geodetic" && TileSize >= 512) nx = 1 << z;
        else if (Profile == "geodetic") nx = 1 << (z + 1);
        else nx = 1 << z;
        return (lon + 180.0) / 360.0 * nx;
    }

    public int Nx(int z)
    {
        if (Profile == "geodetic")
            return TileSize >= 512 ? (1 << z) : (1 << (z + 1));
        return 1 << z;
    }

    public int Ny(int z)
    {
        if (Profile == "geodetic") return Nx(z) / 2;
        return 1 << z;
    }

    public double LonToTx(double lon, int z) => (lon + 180.0) / 360.0 * Nx(z);

    public double LatToTy(double lat, int z)
    {
        lat = Math.Max(-85.0, Math.Min(85.0, lat));
        if (Profile == "geodetic")
            return (lat + 90.0) / 180.0 * Ny(z);
        var r = lat * Math.PI / 180.0;
        var n = 1 << z;
        var slippy = (1.0 - Math.Log(Math.Tan(r) + 1.0 / Math.Cos(r)) / Math.PI) / 2.0 * n;
        return n - 1.0 - slippy;
    }

    public double TxToLon(double tx, int z) => tx / Nx(z) * 360.0 - 180.0;

    public double TyToLat(double ty, int z)
    {
        if (Profile == "geodetic")
            return ty / Ny(z) * 180.0 - 90.0;
        var n = 1 << z;
        var slippy = n - 1.0 - ty;
        return Math.Atan(Math.Sinh(Math.PI - 2.0 * Math.PI * slippy / n)) * 180.0 / Math.PI;
    }

    public string TilePath(int z, int tx, int tyTms) =>
        System.IO.Path.Combine(Path, z.ToString(), tx.ToString(), $"{tyTms}.png");

    public int MinZoom => Zooms.Count > 0 ? Zooms[0] : 0;
    public int MaxZoom => Zooms.Count > 0 ? Zooms[^1] : 20;

    public (double Lat, double Lon) Center() =>
        Bbox.HasValue
            ? ((Bbox.Value.MinLat + Bbox.Value.MaxLat) / 2.0,
               (Bbox.Value.MinLon + Bbox.Value.MaxLon) / 2.0)
            : (0.0, 0.0);

    public int FitZoom(double w, double h)
    {
        if (!Bbox.HasValue) return MinZoom;
        var b = Bbox.Value;
        for (int z = MaxZoom; z >= MinZoom; z--)
        {
            var x0 = LonToTx(b.MinLon, z);
            var x1 = LonToTx(b.MaxLon, z);
            var y0 = LatToTy(b.MinLat, z);
            var y1 = LatToTy(b.MaxLat, z);
            if (Math.Abs(x1 - x0) * DT < w * 0.85 && Math.Abs(y1 - y0) * DT < h * 0.85)
                return z;
        }
        return MinZoom;
    }

    public string Display =>
        Zooms.Count > 0 ? $"{Name}  (z{MinZoom}-{MaxZoom})" : $"{Name}  (z?)";
}
