using System.Reflection;
using System.Windows;

namespace TileViewer.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        VersionText.Text = $"v{version}";
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();
}
