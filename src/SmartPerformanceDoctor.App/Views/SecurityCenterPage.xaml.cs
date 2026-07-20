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

    private void SecurityCardGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var compact = e.NewSize.Width < 760;
        SecurityCardGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        SecurityCardGrid.ColumnDefinitions[1].Width = compact
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        Grid.SetColumn(VaultCard, 0);
        Grid.SetRow(VaultCard, 0);
        Grid.SetColumn(DeleteCard, compact ? 0 : 1);
        Grid.SetRow(DeleteCard, compact ? 1 : 0);

        SecurityHeaderGrid.ColumnDefinitions[1].Width = compact
            ? new GridLength(0)
            : GridLength.Auto;
        Grid.SetColumn(SecurityStatusBadge, compact ? 0 : 1);
        Grid.SetRow(SecurityStatusBadge, compact ? 1 : 0);
        SecurityStatusBadge.HorizontalAlignment = compact
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Stretch;

        for (var index = 0; index < SecurityToolGrid.ColumnDefinitions.Count; index++)
        {
            SecurityToolGrid.ColumnDefinitions[index].Width = compact
                ? new GridLength(index == 0 ? 1 : 0, GridUnitType.Star)
                : new GridLength(1, GridUnitType.Star);
        }

        Grid.SetColumn(VaultIntegrityButton, 0);
        Grid.SetRow(VaultIntegrityButton, 0);
        Grid.SetColumn(ProtectionStatusButton, compact ? 0 : 1);
        Grid.SetRow(ProtectionStatusButton, compact ? 1 : 0);
        Grid.SetColumn(AdvancedRecoveryButton, compact ? 0 : 2);
        Grid.SetRow(AdvancedRecoveryButton, compact ? 2 : 0);
    }
    private void OpenVault(object sender, RoutedEventArgs e) => Navigate(typeof(SecureVaultCenterPage));
    private void OpenSecureDelete(object sender, RoutedEventArgs e) => Navigate(typeof(SecureDeleteCenterPage));
    private void OpenProtection(object sender, RoutedEventArgs e) => Navigate(typeof(ProgramProtectionCenterPage));
    private void OpenAdvanced(object sender, RoutedEventArgs e) => Navigate(typeof(AdvancedCenterPage));
}
