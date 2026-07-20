using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class CareCenterPage : Page
{
    public CareCenterPage() => InitializeComponent();

    private Frame? ShellFrame => App.Shell?.NavigationFrame;

    private void StartQuickCare(object sender, RoutedEventArgs e) =>
        NavigateCare("quick", autoStart: true);

    private void OpenUnifiedCare(object sender, RoutedEventArgs e) =>
        NavigateCare("quick", autoStart: false);

    private void OpenFullCare(object sender, RoutedEventArgs e) =>
        NavigateCare("full", autoStart: false);

    private void OpenDriverCare(object sender, RoutedEventArgs e) =>
        NavigateCare("driver", autoStart: false);

    private void OpenAudioCare(object sender, RoutedEventArgs e) =>
        NavigateCare("audio", autoStart: false);

    private void OpenSystemCare(object sender, RoutedEventArgs e)
    {
        if (ShellFrame is { } frame)
        {
            AppNavigationService.Navigate(frame, typeof(SystemCareCenterPage));
        }
    }

    private void NavigateCare(string scope, bool autoStart)
    {
        if (ShellFrame is { } frame)
        {
            AppNavigationService.NavigateUnifiedCare(frame, scope, autoStart);
        }
    }
}
