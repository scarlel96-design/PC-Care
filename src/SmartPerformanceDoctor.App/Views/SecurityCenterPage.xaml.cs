using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class SecurityCenterPage : Page
{
    public SecurityCenterPage() => InitializeComponent();

    private void Navigate(Type pageType)
    {
        if (App.Shell?.NavigationFrame is { } frame)
        {
            AppNavigationService.Navigate(frame, pageType);
        }
    }

    private void OpenVault(object sender, RoutedEventArgs e) => Navigate(typeof(SecureVaultCenterPage));
    private void OpenSecureDelete(object sender, RoutedEventArgs e) => Navigate(typeof(SecureDeleteCenterPage));
    private void OpenProtection(object sender, RoutedEventArgs e) => Navigate(typeof(ProgramProtectionCenterPage));
    private void OpenAdvanced(object sender, RoutedEventArgs e) => Navigate(typeof(AdvancedCenterPage));
}
