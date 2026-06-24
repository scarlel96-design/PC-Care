using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.ViewModels;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class MacDashboardPage : Page
{
    private readonly DashboardViewModel _viewModel = new();

    public MacDashboardPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.Refresh();
    }

    private void RefreshDashboard(object sender, RoutedEventArgs e) => _viewModel.Refresh();

    private void StartDeepScan(object sender, RoutedEventArgs e)
    {
        var frame = App.Shell?.NavigationFrame;
        if (frame is not null)
        {
            AppNavigationService.NavigateUnifiedCare(
                frame,
                scope: "full",
                autoStart: true,
                includeRepair: true,
                riskAccepted: true);
        }
    }

    private void PrimaryActionClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.PrimaryRecommendation is not null)
        {
            NavigateAction(_viewModel.PrimaryRecommendation);
        }
    }

    private void QuickActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DashboardAction action })
        {
            NavigateAction(action);
        }
    }

    private static void NavigateAction(DashboardAction action)
    {
        var frame = App.Shell?.NavigationFrame;
        if (frame is null)
        {
            return;
        }

        if (action.TargetPage.StartsWith("UnifiedCarePage:", StringComparison.OrdinalIgnoreCase))
        {
            var scope = action.TargetPage.Split(':', 2, StringSplitOptions.TrimEntries)[1];
            AppNavigationService.NavigateUnifiedCare(
                frame,
                scope,
                action.AutoStart,
                action.IncludeRepair,
                action.RiskAccepted);
            return;
        }

        AppNavigationService.TryNavigateByName(frame, action.TargetPage);
    }
}