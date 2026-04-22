using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TileViewer.Models;
using TileViewer.Services;

namespace TileViewer.Views;

public partial class SiteBrowserView : UserControl
{
    public MainWindow? Host { get; set; }
    public string? Src1 { get; private set; }
    public string? Src2 { get; private set; }
    public string Src1DisplayLabel { get; private set; } = "Source 1";
    public string Src2DisplayLabel { get; private set; } = "Source 2";

    private readonly SiteScanner _scanner = new();
    private readonly DecisionsStore _store = new();
    private readonly Dictionary<string, SiteRecord> _sites = new();

    private bool _ready;

    public SiteBrowserView()
    {
        InitializeComponent();
        LoadSourcesFromDisk();
        _ready = true;
    }

    private void LoadSourcesFromDisk()
    {
        _store.LoadSources();
        if (!string.IsNullOrEmpty(_store.Src1))
        {
            Src1 = _store.Src1;
            Src1DisplayLabel = Path.GetFileName(Src1.TrimEnd(Path.DirectorySeparatorChar)) ?? Src1;
            Src1Label.Text = Src1DisplayLabel;
            Src1Label.Foreground = (System.Windows.Media.Brush)FindResource("PeachBrush");
        }
        if (!string.IsNullOrEmpty(_store.Src2))
        {
            Src2 = _store.Src2;
            Src2DisplayLabel = Path.GetFileName(Src2.TrimEnd(Path.DirectorySeparatorChar)) ?? Src2;
            Src2Label.Text = Src2DisplayLabel;
            Src2Label.Foreground = (System.Windows.Media.Brush)FindResource("SapphireBrush");
        }
    }

    private void PickSrc1_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select Source 1 folder" };
        if (dlg.ShowDialog() != true) return;
        Src1 = dlg.FolderName;
        Src1DisplayLabel = Path.GetFileName(Src1.TrimEnd(Path.DirectorySeparatorChar)) ?? Src1;
        Src1Label.Text = Src1DisplayLabel;
        Src1Label.Foreground = (System.Windows.Media.Brush)FindResource("PeachBrush");
        _store.Src1 = Src1;
        _store.SaveSources();
    }

    private void PickSrc2_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select Source 2 folder" };
        if (dlg.ShowDialog() != true) return;
        Src2 = dlg.FolderName;
        Src2DisplayLabel = Path.GetFileName(Src2.TrimEnd(Path.DirectorySeparatorChar)) ?? Src2;
        Src2Label.Text = Src2DisplayLabel;
        Src2Label.Foreground = (System.Windows.Media.Brush)FindResource("SapphireBrush");
        _store.Src2 = Src2;
        _store.SaveSources();
    }

    public void Scan()
    {
        if (string.IsNullOrEmpty(Src1) && string.IsNullOrEmpty(Src2))
        {
            MessageBox.Show("Pick at least one source folder first.", "No Sources",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ProgressLabel.Text = "Scanning...";
        ProgressLabel.Foreground = (System.Windows.Media.Brush)FindResource("RedBrush");
        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

        var (sites, errors) = _scanner.Scan(Src1, Src2);
        _sites.Clear();
        foreach (var (k, v) in sites) _sites[k] = v;
        _store.Src1 = Src1;
        _store.Src2 = Src2;
        _store.ApplyDecisions(_sites);
        Populate();
        UpdateProgress();
        if (errors.Count > 0)
            MessageBox.Show(string.Join("\n", errors), "Scan Notes",
                MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Scan_Click(object sender, RoutedEventArgs e) => Scan();

    private void Populate()
    {
        var search = (SearchBox.Text ?? "").ToLowerInvariant();
        var statusFilter = ((ComboBoxItem)StatusFilter.SelectedItem)?.Content?.ToString() ?? "all";
        var rows = new List<SiteRow>();
        foreach (var s in _sites.Values.OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(search) &&
                !s.DisplayName.ToLowerInvariant().Contains(search) &&
                !s.Key.Contains(search))
                continue;
            if (statusFilter == "undecided" && !string.IsNullOrEmpty(s.Decision)) continue;
            if (statusFilter != "all" && statusFilter != "undecided" && s.Status != statusFilter) continue;
            rows.Add(new SiteRow(s));
        }
        SitesList.ItemsSource = rows;
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) { if (_ready) Populate(); }
    private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (_ready) Populate(); }

    private void SitesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SitesList.SelectedItem is SiteRow row && Host != null)
            Host.CompareSite(row.Record);
    }

    private void SetDecision(string decision)
    {
        if (SitesList.SelectedItem is not SiteRow row) return;
        row.Record.Decision = decision;
        var oldKey = row.Record.Key;
        Populate();
        var items = (List<SiteRow>?)SitesList.ItemsSource;
        if (items == null) return;
        var idx = items.FindIndex(r => r.Record.Key == oldKey);
        if (idx >= 0)
        {
            for (int i = idx + 1; i < items.Count; i++)
            {
                if (string.IsNullOrEmpty(items[i].Record.Decision))
                {
                    SitesList.SelectedIndex = i;
                    SitesList.ScrollIntoView(items[i]);
                    return;
                }
            }
            SitesList.SelectedIndex = idx;
        }
        _store.SaveDecisions(_sites);
        UpdateProgress();
    }

    private void KeepOne_Click(object sender, RoutedEventArgs e) => SetDecision("keep-1");
    private void KeepTwo_Click(object sender, RoutedEventArgs e) => SetDecision("keep-2");
    private void Skip_Click(object sender, RoutedEventArgs e) => SetDecision("skip");
    private void ClearDec_Click(object sender, RoutedEventArgs e) => SetDecision("");

    private void UpdateProgress()
    {
        var total = _sites.Count;
        var decided = _sites.Values.Count(s => !string.IsNullOrEmpty(s.Decision));
        var both = _sites.Values.Count(s => s.Status == "both");
        ProgressLabel.Text = $"{decided}/{total} decided  ({both} overlap)";
        ProgressLabel.Foreground = (System.Windows.Media.Brush)FindResource("Overlay0Brush");
    }

    public class SiteRow : INotifyPropertyChanged
    {
        public SiteRecord Record { get; }
        public SiteRow(SiteRecord r) { Record = r; }
        public string DisplayName => Record.DisplayName;
        public string StatusText => Record.Status switch
        {
            "both" => "1+2",
            "1-only" => "1",
            "2-only" => "2",
            _ => Record.Status
        };
        public string DecisionText => Record.Decision switch
        {
            "keep-1" => "Keep 1",
            "keep-2" => "Keep 2",
            "skip" => "Skip",
            _ => ""
        };
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
