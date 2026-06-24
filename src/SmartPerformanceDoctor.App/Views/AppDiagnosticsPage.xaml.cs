using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class AppDiagnosticsPage : Page
{
    private readonly AppSelfCheckService _selfCheckService = new();

    public AppDiagnosticsPage()
    {
        InitializeComponent();
        Refresh();
    }

    private void RefreshDiagnostics(object sender, RoutedEventArgs e)
    {
        Refresh();
    }

    private void OpenAppFolder(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = AppContext.BaseDirectory,
            UseShellExecute = true
        });
    }

    private void Refresh()
    {
        DiagnosticsList.ItemsSource = _selfCheckService.Run();
    }
}
