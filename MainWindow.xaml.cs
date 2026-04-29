using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TileViewer.Models;
using TileViewer.Views;

namespace TileViewer;

public partial class MainWindow : Window
{
    public List<TileSet> Tilesets { get; } = new();
    public List<MapPanelView> Panels { get; } = new();
    public TileCache Cache { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        Browser.Host = this;

        AddPanel();
        AddPanel();

        Loaded += OnLoaded;

        // Ctrl+N new panel, Ctrl+B toggle browser
        var newPanel = new RoutedCommand();
        newPanel.InputGestures.Add(new KeyGesture(Key.N, ModifierKeys.Control));
        CommandBindings.Add(new CommandBinding(newPanel, (_, _) => AddPanel()));
        InputBindings.Add(new KeyBinding(newPanel, Key.N, ModifierKeys.Control));

        var toggleBrowser = new RoutedCommand();
        CommandBindings.Add(new CommandBinding(toggleBrowser, (_, _) => ToggleBrowser()));
        InputBindings.Add(new KeyBinding(toggleBrowser, Key.B, ModifierKeys.Control));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Load any tileset paths passed on command line
        var args = Environment.GetCommandLineArgs().Skip(1).ToList();
        for (int i = 0; i < args.Count; i++)
        {
            if (Directory.Exists(args[i]))
            {
                while (i >= Panels.Count) AddPanel();
                LoadAndAssign(args[i], Panels[i]);
            }
        }
    }

    private void AddPanel_Click(object sender, RoutedEventArgs e) => AddPanel();

    public MapPanelView AddPanel()
    {
        var p = new MapPanelView { Host = this };
        Panels.Add(p);
        Layout();
        p.RefreshCombo();
        return p;
    }

    public void RemovePanel(MapPanelView panel)
    {
        if (Panels.Count <= 1) return;
        Panels.Remove(panel);
        Layout();
    }

    private void Layout()
    {
        PanelHost.Children.Clear();
        PanelHost.ColumnDefinitions.Clear();
        for (int i = 0; i < Panels.Count; i++)
        {
            PanelHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var p = Panels[i];
            Grid.SetColumn(p, i);
            p.Margin = new Thickness(1);
            PanelHost.Children.Add(p);
        }
    }

    public void LoadAndAssign(string path, MapPanelView panel)
    {
        TileSet? ts = Tilesets.FirstOrDefault(t =>
            string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
        if (ts == null)
        {
            ts = new TileSet(path);
            if (ts.Zooms.Count == 0)
            {
                MessageBox.Show($"No zoom folders in:\n{path}", "No tiles",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Tilesets.Add(ts);
            StatusText.Text = $"{Tilesets.Count} tileset(s)";
            foreach (var p in Panels) p.RefreshCombo();
        }
        panel.Assign(ts);
    }

    public void SyncOthers(MapPanelView source)
    {
        if (SyncCheck.IsChecked != true) return;
        foreach (var p in Panels)
        {
            if (p == source || p.Tileset == null) continue;
            p.Lat = source.Lat;
            p.Lon = source.Lon;
            p.Zoom = Math.Max(p.Tileset.MinZoom, Math.Min(p.Tileset.MaxZoom, source.Zoom));
            p.ScheduleRender();
        }
    }

    public void CompareSite(Models.SiteRecord site)
    {
        var lbl1 = $"[1] {Browser.Src1DisplayLabel}";
        var lbl2 = $"[2] {Browser.Src2DisplayLabel}";

        foreach (var p in Panels) p.Clear();
        while (Panels.Count < 2 && site.Status == "both") AddPanel();

        if (site.Status == "both")
        {
            Panels[0].SetDriveLabel(lbl1);
            LoadAndAssign(site.Path1!, Panels[0]);
            Panels[1].SetDriveLabel(lbl2);
            LoadAndAssign(site.Path2!, Panels[1]);
            Panels[1].Lat = Panels[0].Lat;
            Panels[1].Lon = Panels[0].Lon;
            Panels[1].Zoom = Panels[0].Zoom;
            Panels[1].ScheduleRender();
        }
        else if (site.Path1 != null)
        {
            Panels[0].SetDriveLabel(lbl1);
            LoadAndAssign(site.Path1, Panels[0]);
        }
        else if (site.Path2 != null)
        {
            Panels[0].SetDriveLabel(lbl2);
            LoadAndAssign(site.Path2, Panels[0]);
        }
    }

    private void AboutClick(object sender, MouseButtonEventArgs e)
    {
        new Views.AboutWindow { Owner = this }.ShowDialog();
    }

    private void ToggleBrowser()
    {
        if (SidebarCol.Width.Value > 0)
        {
            _priorSidebarWidth = SidebarCol.Width;
            SidebarCol.Width = new GridLength(0);
            SidebarCol.MinWidth = 0;
        }
        else
        {
            SidebarCol.MinWidth = 200;
            SidebarCol.Width = _priorSidebarWidth;
        }
    }
    private GridLength _priorSidebarWidth = new(320);
}
