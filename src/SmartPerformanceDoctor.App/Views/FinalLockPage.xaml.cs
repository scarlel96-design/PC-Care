using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.ViewModels;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class FinalLockPage : Page
{
    private readonly FinalLockViewModel _viewModel = new();

    public FinalLockPage()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.Evaluate();
    }

    private void RunFinalLock(object sender, RoutedEventArgs e)
    {
        _viewModel.Evaluate();
    }
}
