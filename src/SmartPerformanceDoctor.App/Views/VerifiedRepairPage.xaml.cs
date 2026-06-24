using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.ViewModels;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class VerifiedRepairPage : Page
{
    private readonly VerifiedRepairViewModel _viewModel = new();

    public VerifiedRepairPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.Load();
        PlanBox.SelectedIndex = 0;
    }

    private void PlanChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlanBox.SelectedItem is IntelligentRepairPlan plan)
        {
            _viewModel.SelectedPlan = plan;
        }
    }

    private async void RunDryVerify(object sender, RoutedEventArgs e)
    {
        await _viewModel.RunAsync(apply: false, CancellationToken.None);
    }

    private async void RunApplyVerify(object sender, RoutedEventArgs e)
    {
        await _viewModel.RunAsync(apply: true, CancellationToken.None);
    }
}
