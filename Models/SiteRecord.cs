namespace TileViewer.Models;

public class SiteRecord
{
    public string Key { get; }
    public string DisplayName { get; set; }
    public string? Path1 { get; set; }
    public string? Path2 { get; set; }
    public string Decision { get; set; } = "";

    public SiteRecord(string key, string displayName = "")
    {
        Key = key;
        DisplayName = displayName;
    }

    public string Status =>
        (Path1 != null && Path2 != null) ? "both" :
        (Path1 != null) ? "1-only" : "2-only";
}
