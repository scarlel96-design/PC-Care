using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.ViewModels;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class DriverRepairPage : Page
{
    private readonly RepairWorkbenchViewModel _viewModel = new();

    public DriverRepairPage()
    {
        InitializeComponent();
        ActionBox.ItemsSource = RepairActionRegistry.DriverActions;
        ActionBox.SelectedIndex = 0;
        UpdateActionInfo();
    }

    private void ActionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionInfo();
    }

    private async void RunDry(object sender, RoutedEventArgs e)
    {
        await Run(dryRun: true);
    }

    private async void RunApply(object sender, RoutedEventArgs e)
    {
        await Run(dryRun: false);
    }

    private async Task Run(bool dryRun)
    {
        if (ActionBox.SelectedItem is not RepairActionDescriptor action)
        {
            return;
        }

        await _viewModel.RunAsync(action, TargetBox.Text, dryRun, RiskCheck.IsChecked == true, CancellationToken.None);
        StatusText.Text = _viewModel.Status;
        ResultText.Text = _viewModel.Result;
    }

    private void UpdateActionInfo()
    {
        if (ActionBox.SelectedItem is not RepairActionDescriptor action)
        {
            return;
        }

        DescriptionText.Text = $"{action.Description}\n위험도: {action.Risk}";
        TargetHintText.Text = action.TargetHint;
        TargetBox.IsEnabled = action.RequiresTarget;
        TargetBox.Text = action.RequiresTarget ? TargetBox.Text : action.DefaultTarget;
    }
}
