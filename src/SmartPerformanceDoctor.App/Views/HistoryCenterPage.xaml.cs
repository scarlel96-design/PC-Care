using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class HistoryCenterPage : Page
{
    public HistoryCenterPage() => InitializeComponent();

    private void Navigate(Type pageType)
    {
        if (App.Shell?.NavigationFrame is { } frame)
        {
            AppNavigationService.Navigate(frame, pageType);
        }
    }

    private void OpenReports(object sender, RoutedEventArgs e) => Navigate(typeof(ReportPage));
    private void OpenActivity(object sender, RoutedEventArgs e) => Navigate(typeof(EvidenceExplorerPage));
    private void OpenRepairLogs(object sender, RoutedEventArgs e) => Navigate(typeof(RepairLogPage));
}
