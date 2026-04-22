using System.IO;
using System.Text.Json;
using TileViewer.Models;

namespace TileViewer.Services;

public class DecisionsStore
{
    public static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".tileviewer_decisions.json");

    public string? Src1 { get; set; }
    public string? Src2 { get; set; }

    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public void LoadSources()
    {
        if (!File.Exists(FilePath)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (doc.RootElement.TryGetProperty("sources", out var s))
            {
                if (s.TryGetProperty("src_1", out var a) && a.ValueKind == JsonValueKind.String)
                {
                    var v = a.GetString();
                    if (!string.IsNullOrEmpty(v) && Directory.Exists(v)) Src1 = v;
                }
                if (s.TryGetProperty("src_2", out var b) && b.ValueKind == JsonValueKind.String)
                {
                    var v = b.GetString();
                    if (!string.IsNullOrEmpty(v) && Directory.Exists(v)) Src2 = v;
                }
            }
        }
        catch { }
    }

    public void ApplyDecisions(Dictionary<string, SiteRecord> sites)
    {
        if (!File.Exists(FilePath)) return;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (!doc.RootElement.TryGetProperty("decisions", out var decs)) return;
            foreach (var prop in decs.EnumerateObject())
            {
                if (!sites.TryGetValue(prop.Name, out var rec)) continue;
                if (prop.Value.TryGetProperty("decision", out var d) && d.ValueKind == JsonValueKind.String)
                {
                    var dec = d.GetString() ?? "";
                    rec.Decision = dec switch
                    {
                        "keep-e" => "keep-1",
                        "keep-f" => "keep-2",
                        _ => dec
                    };
                }
            }
        }
        catch { }
    }

    public void SaveSources()
    {
        var root = ReadOrEmpty();
        root["sources"] = new Dictionary<string, object?>
        {
            ["src_1"] = Src1,
            ["src_2"] = Src2,
        };
        Write(root);
    }

    public void SaveDecisions(Dictionary<string, SiteRecord> sites)
    {
        var root = ReadOrEmpty();
        root["version"] = 1;
        root["sources"] = new Dictionary<string, object?>
        {
            ["src_1"] = Src1,
            ["src_2"] = Src2,
        };
        var decs = new Dictionary<string, object>();
        foreach (var (key, s) in sites)
        {
            if (string.IsNullOrEmpty(s.Decision)) continue;
            decs[key] = new Dictionary<string, object?>
            {
                ["decision"] = s.Decision,
                ["path_1"] = s.Path1,
                ["path_2"] = s.Path2,
                ["timestamp"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            };
        }
        root["decisions"] = decs;
        Write(root);
    }

    private static Dictionary<string, object> ReadOrEmpty()
    {
        if (!File.Exists(FilePath)) return new();
        try
        {
            var txt = File.ReadAllText(FilePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(txt);
            return dict ?? new();
        }
        catch { return new(); }
    }

    private static void Write(Dictionary<string, object> root)
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(root, Opts)); }
        catch { }
    }
}
