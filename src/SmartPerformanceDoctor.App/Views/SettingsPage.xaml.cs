using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class SettingsPage : Page
{
    private readonly UserModeService _userMode = new();

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var current = _userMode.Current.ToString();
        foreach (var item in UserModeBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Tag as string, current, StringComparison.OrdinalIgnoreCase))
            {
                UserModeBox.SelectedItem = item;
                break;
            }
        }

        UpdateHint();
    }

    private void UserModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (UserModeBox.SelectedItem is not ComboBoxItem item || item.Tag is not string tag)
        {
            return;
        }

        if (Enum.TryParse<UserMode>(tag, true, out var mode))
        {
            _userMode.Save(mode);
            App.Shell?.RefreshNavigationVisibility();
            UpdateHint();
        }
    }

    private void UpdateHint()
    {
        ModeHint.Text = _userMode.Current switch
        {
            UserMode.Basic => "일반 메뉴만 표시됩니다. 내부 엔진·로그 항목은 숨겨집니다.",
            UserMode.Advanced => "고급 기능 센터와 상세 로그에 접근할 수 있습니다.",
            UserMode.Developer => "릴리즈 게이트, Final Lock, raw 로그 도구가 표시됩니다.",
            _ => ""
        };
    }

    private Frame? ShellFrame => App.Shell?.NavigationFrame;

    private void OpenFeatureManagement(object sender, RoutedEventArgs e)
    {
        ShellFrame?.Navigate(typeof(FeatureManagementPage));
    }

    private void OpenAdvancedCenter(object sender, RoutedEventArgs e)
    {
        ShellFrame?.Navigate(typeof(AdvancedCenterPage));
    }

    private void OpenUpdate(object sender, RoutedEventArgs e)
    {
        if (ShellFrame is not null)
        {
            AppNavigationService.TryNavigateByName(ShellFrame, "UpdateStatusPage");
        }
    }

    private void OpenFirstRun(object sender, RoutedEventArgs e)
    {
        ShellFrame?.Navigate(typeof(FirstRunPage));
    }

    private void OpenProgramProtection(object sender, RoutedEventArgs e)
    {
        ShellFrame?.Navigate(typeof(ProgramProtectionCenterPage));
    }
}