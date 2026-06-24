using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.ViewModels;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class ProgressHudPage : Page
{
    private readonly ProgressHudViewModel _viewModel = new();

    public ProgressHudPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
    }

    private void RefreshProgress(object sender, RoutedEventArgs e)
    {
        _viewModel.Refresh();
    }

    private void SimulateCoreProbe(object sender, RoutedEventArgs e)
    {
        _viewModel.SimulateCoreProbe();
    }

    private void ClearProgress(object sender, RoutedEventArgs e)
    {
        _viewModel.Clear();
    }
}
