using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SmartPerformanceDoctor.App.Branding;
using SmartPerformanceDoctor.App.Controls;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.Services.Installation;
using SmartPerformanceDoctor.App.Views;
using SmartPerformanceDoctor.Contracts.Models.Installation;
using WinRT.Interop;

namespace SmartPerformanceDoctor.App;

public sealed partial class MainWindow : Window
{
    private AppWindow? _appWindow;
    private readonly InstalledFeaturesService _installedFeatures = new();
    private readonly UserModeService _userMode = new();

    public Frame NavigationFrame => RootFrame;

    public MainWindow()
    {
        InitializeComponent();
        UiDispatcher.Queue = DispatcherQueue.GetForCurrentThread();
        VersionText.Text = $"버전 {AppInfo.BuildVersion}";
        ExtendsContentIntoTitleBar = true;
        RootFrame.NavigationFailed += OnNavigationFailed;
        Activated += OnWindowActivated;
        _userMode.ModeChanged += (_, _) => RefreshNavigationVisibility();
        BindWindowControls();
        ApplyStartupTrustStatus();
        AppNavigationService.NavigateDashboard(RootFrame);
        RefreshNavigationVisibility();
    }

    public void ApplyStartupTrustStatus()
    {
        TitleStatusText.Text = StartupTrustStatusService.BuildTitleStatus();
    }

    public void RefreshNavigationVisibility()
    {
        _installedFeatures.Refresh();
        _userMode.Load();

        foreach (var button in EnumerateNavButtons())
        {
            button.Visibility = IsNavVisible(button) ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdateSectionVisibility();
        SearchChanged(SearchBox, null!);
    }

    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        ConfigureCustomTitleBar();
    }

    private void BindWindowControls()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        ConfigureCustomTitleBar();

        WindowControls.CloseRequested += (_, _) => Close();
        WindowControls.MinimizeRequested += (_, _) =>
        {
            if (_appWindow?.Presenter is OverlappedPresenter presenter)
            {
                presenter.Minimize();
            }
        };
        WindowControls.MaximizeRequested += (_, _) =>
        {
            if (_appWindow?.Presenter is OverlappedPresenter presenter)
            {
                if (presenter.State == OverlappedPresenterState.Maximized)
                {
                    presenter.Restore();
                }
                else
                {
                    presenter.Maximize();
                }
            }
        };
    }

    private void ConfigureCustomTitleBar()
    {
        if (_appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(true, false);
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        SetTitleBar(AppTitleBar);

        if (_appWindow is null || !AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = _appWindow.TitleBar;
        titleBar.ExtendsContentIntoTitleBar = true;
        titleBar.PreferredHeightOption = TitleBarHeightOption.Standard;
        var transparent = ColorHelper.FromArgb(0, 0, 0, 0);
        titleBar.ButtonBackgroundColor = transparent;
        titleBar.ButtonInactiveBackgroundColor = transparent;
        titleBar.ButtonForegroundColor = transparent;
        titleBar.ButtonInactiveForegroundColor = transparent;
        titleBar.ButtonHoverBackgroundColor = transparent;
        titleBar.ButtonHoverForegroundColor = transparent;
        titleBar.ButtonPressedBackgroundColor = transparent;
        titleBar.ButtonPressedForegroundColor = transparent;
        titleBar.BackgroundColor = ColorHelper.FromArgb(255, 245, 245, 247);
        titleBar.InactiveBackgroundColor = ColorHelper.FromArgb(255, 245, 245, 247);
        titleBar.ForegroundColor = ColorHelper.FromArgb(255, 60, 60, 67);
        titleBar.InactiveForegroundColor = ColorHelper.FromArgb(255, 134, 134, 139);
    }

    private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        var detail = e.Exception?.Message ?? "알 수 없는 오류";
        CrashCaptureService.WriteCrash("navigation-failed", e.Exception, $"{e.SourcePageType.Name}: {detail}");
        ShowNavFeedback($"화면 전환 실패: {detail}");
        e.Handled = true;
    }

    private void ShowNavFeedback(string message)
    {
        TitleStatusText.Text = $"{DateTime.Now:HH:mm:ss} · {message}";
    }

    private void Navigate(Action navigate, string label)
    {
        try
        {
            ShowNavFeedback($"{label}…");
            navigate();
            ShowNavFeedback($"{label} 완료");
        }
        catch (Exception ex)
        {
            ShowNavFeedback($"{label} 오류: {ex.Message}");
        }
    }

    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        var query = (SearchBox.Text ?? string.Empty).Trim();
        var hasQuery = query.Length > 0;

        foreach (var button in EnumerateNavButtons())
        {
            var featureVisible = IsNavVisible(button);
            var searchVisible = !hasQuery || (button.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
            button.Visibility = featureVisible && searchVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdateSectionVisibility();
    }

    private bool IsNavVisible(MacNavigationButton button) =>
        FeatureVisibilityService.IsNavVisible(button.Tag as string, _installedFeatures, _userMode);

    private void UpdateSectionVisibility()
    {
        TextBlock? currentSection = null;
        var sectionHasVisible = false;

        foreach (var child in NavPanel.Children)
        {
            if (child is TextBlock section && (section.Tag as string)?.StartsWith("section:", StringComparison.Ordinal) == true)
            {
                if (currentSection is not null)
                {
                    currentSection.Visibility = sectionHasVisible ? Visibility.Visible : Visibility.Collapsed;
                }

                currentSection = section;
                sectionHasVisible = FeatureVisibilityService.IsSectionVisible(section.Tag as string ?? "", _userMode);
                continue;
            }

            if (child is MacNavigationButton button && button.Visibility == Visibility.Visible)
            {
                sectionHasVisible = true;
            }
        }

        if (currentSection is not null)
        {
            currentSection.Visibility = sectionHasVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private IEnumerable<MacNavigationButton> EnumerateNavButtons()
    {
        foreach (var child in NavPanel.Children)
        {
            if (child is MacNavigationButton button)
            {
                yield return button;
            }
        }
    }

    private void OpenDashboard(object sender, RoutedEventArgs e) =>
        Navigate(() => AppNavigationService.NavigateDashboard(RootFrame), "홈");

    private void OpenUnifiedCare(object sender, RoutedEventArgs e) =>
        Navigate(() => AppNavigationService.NavigateUnifiedCare(RootFrame, "quick", autoStart: false), AstraCareBranding.Repair);

    private void OpenReports(object sender, RoutedEventArgs e) =>
        Navigate(() => RootFrame.Navigate(typeof(ReportPage)), "보고서");

    private void OpenActivity(object sender, RoutedEventArgs e) =>
        Navigate(() => RootFrame.Navigate(typeof(EvidenceExplorerPage)), "작업 내역");

    private void OpenSettings(object sender, RoutedEventArgs e) =>
        Navigate(() => RootFrame.Navigate(typeof(SettingsPage)), "환경설정");

    private void OpenAdvancedCenter(object sender, RoutedEventArgs e) =>
        Navigate(() => RootFrame.Navigate(typeof(AdvancedCenterPage)), "고급 도구");

    private void OpenSystemCare(object sender, RoutedEventArgs e) =>
        Navigate(() => RootFrame.Navigate(typeof(SystemCareCenterPage)), AstraCareBranding.Clean);

    private void OpenSecureVault(object sender, RoutedEventArgs e) =>
        Navigate(() => RootFrame.Navigate(typeof(SecureVaultCenterPage)), AstraCareBranding.VaultNav);

    private void OpenSecureDelete(object sender, RoutedEventArgs e) =>
        Navigate(() => RootFrame.Navigate(typeof(SecureDeleteCenterPage)), AstraCareBranding.ShredNav);

    private void OpenFeatureManagement(object sender, RoutedEventArgs e) =>
        Navigate(() => RootFrame.Navigate(typeof(FeatureManagementPage)), "기능 관리");
}