using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class ReleaseStatusPage : Page
{
    private readonly QualityGateService _qualityGateService = new();

    public ReleaseStatusPage()
    {
        InitializeComponent();
        Refresh();
    }

    private void RefreshQualityGates(object sender, RoutedEventArgs e)
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
        GateList.ItemsSource = _qualityGateService.Evaluate();
    }
}
