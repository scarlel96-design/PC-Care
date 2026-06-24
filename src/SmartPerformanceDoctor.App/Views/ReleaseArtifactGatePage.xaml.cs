using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.ViewModels;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class ReleaseArtifactGatePage : Page
{
    private readonly ReleaseArtifactGateViewModel _viewModel = new();

    public ReleaseArtifactGatePage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.Evaluate();
    }

    private void RunGate(object sender, RoutedEventArgs e)
    {
        _viewModel.Evaluate();
    }
}
