using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.ViewModels;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class RepairHelperE2EGatePage : Page
{
    private readonly RepairHelperE2EGateViewModel _viewModel = new();

    public RepairHelperE2EGatePage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private async void RunGate(object sender, RoutedEventArgs e)
    {
        await _viewModel.RunAsync(CancellationToken.None);
    }
}
