using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class FirstRunPage : Page
{
    private readonly FirstRunService _firstRunService = new();

    public FirstRunPage()
    {
        InitializeComponent();
        if (_firstRunService.IsFirstRun)
        {
            ResultList.ItemsSource = _firstRunService.RunSetup();
        }
    }

    private void RunFirstSetup(object sender, RoutedEventArgs e)
    {
        ResultList.ItemsSource = _firstRunService.RunSetup();
    }
}
