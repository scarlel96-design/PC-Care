using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class SelfHealingPage : Page
{
    private readonly SelfHealingService _selfHealingService = new();

    public SelfHealingPage()
    {
        InitializeComponent();
        ResultList.ItemsSource = _selfHealingService.InspectAndRepair();
    }

    private void RunSelfHealing(object sender, RoutedEventArgs e)
    {
        ResultList.ItemsSource = _selfHealingService.InspectAndRepair();
    }
}
