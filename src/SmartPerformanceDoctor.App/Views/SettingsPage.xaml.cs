using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.Aegis;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.Services.Update;
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
        var bg = BackgroundRunPreferences.Load();
        bg.RunAtWindowsStartup = WindowsStartupRegistration.IsEnabled();
        bg.Save();
        StartupToggle.IsOn = bg.RunAtWindowsStartup;
        BackgroundToggle.IsOn = bg.RunInBackgroundOnClose;
        UpdateStartupStatusLine();
        var autoUpdate = AutoUpdateCheckPreferences.Load();
        AutoUpdateToggle.IsOn = autoUpdate.Enabled;
    }

    private void AutoUpdateToggleChanged(object sender, RoutedEventArgs e)
    {
        var prefs = AutoUpdateCheckPreferences.Load();
        prefs.Enabled = AutoUpdateToggle.IsOn;
        prefs.Save();
    }

    private void StartupToggleChanged(object sender, RoutedEventArgs e)
    {
        var prefs = BackgroundRunPreferences.Load();
        prefs.RunAtWindowsStartup = StartupToggle.IsOn;
        prefs.Save();
        var exe = InstalledAppPaths.ResolveClientExecutable();
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            StartupStatusText.Text = "실행 파일 경로를 찾지 못해 시작 프로그램 등록에 실패했습니다.";
            StartupToggle.IsOn = false;
            prefs.RunAtWindowsStartup = false;
            prefs.Save();
            return;
        }

        WindowsStartupRegistration.SetEnabled(prefs.RunAtWindowsStartup, exe);
        UpdateStartupStatusLine();
    }

    private void UpdateStartupStatusLine()
    {
        StartupStatusText.Text = WindowsStartupRegistration.IsEnabled()
            ? "Windows 시작 시 백그라운드로 실행되도록 등록되어 있습니다."
            : "시작 프로그램 등록이 해제되어 있습니다.";
    }

    private void BackgroundToggleChanged(object sender, RoutedEventArgs e)
    {
        var prefs = BackgroundRunPreferences.Load();
        prefs.RunInBackgroundOnClose = BackgroundToggle.IsOn;
        prefs.Save();
        TrayIconService.Shared.EnsureInitialized();
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
        try
        {
            ShellFrame?.Navigate(typeof(UpdateStatusPage));
        }
        catch (Exception ex)
        {
            CrashCaptureService.WriteCrash("open-update-page", ex, ex.Message);
        }
    }

    private void OpenFirstRun(object sender, RoutedEventArgs e)
    {
        ShellFrame?.Navigate(typeof(FirstRunPage));
    }

}