using System.IO;
using System.Text.RegularExpressions;
using TileViewer.Models;

namespace TileViewer.Services;

public class SiteScanner
{
    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
        { "RawData", "__pycache__", ".git", "build", "dist", "bin", "obj" };
    private const int MaxDepth = 5;

    private static readonly Regex SplitCamel1 = new(@"([a-z])([A-Z])");
    private static readonly Regex SplitCamel2 = new(@"([A-Z]+)([A-Z][a-z])");

    public static string Normalize(string name)
    {
        var s = SplitCamel1.Replace(name, "$1 $2");
        s = SplitCamel2.Replace(s, "$1 $2");
        s = s.Replace("_", " ").ToLowerInvariant();
        return string.Join("_", s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static string ToPascal(string name) =>
        string.Concat(name.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    public (Dictionary<string, SiteRecord> Sites, List<string> Errors) Scan(string? src1, string? src2)
    {
        var sites = new Dictionary<string, SiteRecord>();
        var errors = new List<string>();
        if (!string.IsNullOrEmpty(src1)) ScanSource(src1, 1, sites, errors);
        if (!string.IsNullOrEmpty(src2)) ScanSource(src2, 2, sites, errors);
        return (sites, errors);
    }

    private static void ScanSource(string root, int srcNum,
        Dictionary<string, SiteRecord> sites, List<string> errors)
    {
        if (!Directory.Exists(root))
        {
            errors.Add($"Source {srcNum}: {root} not found");
            return;
        }
        var rootDepth = root.TrimEnd(Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar).Split(Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar).Length;
        Walk(root, srcNum, root, rootDepth, sites);
    }

    private static void Walk(string root, int srcNum, string dir, int rootDepth,
        Dictionary<string, SiteRecord> sites)
    {
        int depth;
        try
        {
            depth = dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length - rootDepth;
        }
        catch { return; }

        if (depth >= MaxDepth) return;

        string[] files;
        string[] subdirs;
        try
        {
            files = Directory.GetFiles(dir);
            subdirs = Directory.GetDirectories(dir);
        }
        catch { return; }

        if (files.Any(f => Path.GetFileName(f).Equals("tilemapresource.xml", StringComparison.OrdinalIgnoreCase)))
        {
            var siteName = DeriveSiteName(root, dir);
            if (!string.IsNullOrEmpty(siteName))
                Register(dir, siteName, srcNum, sites);
            return;
        }

        foreach (var sub in subdirs)
        {
            var name = Path.GetFileName(sub);
            if (SkipDirs.Contains(name)) continue;
            if (int.TryParse(name, out _)) continue;
            Walk(root, srcNum, sub, rootDepth, sites);
        }
    }

    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase)
        { "tiles", "tile", "data" };

    private static string? DeriveSiteName(string root, string tilePath)
    {
        string rel;
        try { rel = Path.GetRelativePath(root, tilePath); }
        catch { return Path.GetFileName(tilePath); }

        var parts = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(p => !string.IsNullOrEmpty(p) && p != ".").ToArray();
        if (parts.Length == 0) return null;
        for (int i = parts.Length - 1; i >= 0; i--)
            if (!GenericNames.Contains(parts[i])) return parts[i];
        return parts[0];
    }

    private static readonly Regex PascalLead = new(@"^[A-Z][a-z]");

    private static void Register(string tilePath, string siteName, int srcNum,
        Dictionary<string, SiteRecord> sites)
    {
        var key = Normalize(siteName);
        if (!sites.TryGetValue(key, out var rec))
        {
            rec = new SiteRecord(key, ToPascal(key));
            sites[key] = rec;
        }
        if (srcNum == 1) rec.Path1 = tilePath;
        else rec.Path2 = tilePath;
        if (PascalLead.IsMatch(siteName)) rec.DisplayName = siteName;
    }
}
