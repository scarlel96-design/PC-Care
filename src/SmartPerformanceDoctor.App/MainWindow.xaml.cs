using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using SmartPerformanceDoctor.Aegis;
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
    private bool _titleBarConfigured;
    private bool _forceShutdown;
    private bool _closingHooked;
    private InstalledFeaturesService _installedFeatures = null!;
    private UserModeService _userMode = null!;

    private Grid AppTitleBar = null!;
    private Image TitleBarIcon = null!;
    private TextBlock TitleStatusText = null!;
    private TextBlock VersionText = null!;
    private TextBox SearchBox = null!;
    private StackPanel NavPanel = null!;
    private Frame RootFrame = null!;

    public Frame NavigationFrame => RootFrame;

    public MainWindow()
    {
        StartupDiagnostics.Write("mainwindow", "ctor-begin");
        InitializeComponent();
        StartupDiagnostics.Write("mainwindow", "xaml-init-ok");
        _installedFeatures = new InstalledFeaturesService();
        _userMode = new UserModeService();
        UiDispatcher.Queue = DispatcherQueue.GetForCurrentThread();
        RootFrame = new Frame();
        Content = RootFrame;
        StartupDiagnostics.Write("mainwindow", "init-ok");
        ExtendsContentIntoTitleBar = true;
        RootFrame.NavigationFailed += OnNavigationFailed;
        Activated += OnWindowActivated;
        _userMode.ModeChanged += (_, _) => RefreshNavigationVisibility();

        DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
        {
            try
            {
                StartupDiagnostics.Write("mainwindow", "deferred-begin");
                BuildApplicationShell();
                if (Application.Current.Resources.TryGetValue("MacSearchBoxStyle", out var searchStyle)
                    && searchStyle is Style macSearchStyle)
                {
                    SearchBox.Style = macSearchStyle;
                }

                VersionText.Text = $"버전 {AppInfo.BuildVersion}";
                BuildDefaultNavigation();
                BindWindowControls();
                ApplyProductIcon();
                ApplyStartupTrustStatus();
                AppNavigationService.NavigateDashboard(RootFrame);
                RefreshNavigationVisibility();
                StartupDiagnostics.Write("mainwindow", "deferred-ok");
            }
            catch (Exception ex)
            {
                StartupDiagnostics.Write("mainwindow-deferred-fail", ex.Message);
                CrashCaptureService.WriteCrash("mainwindow-deferred", ex, ex.ToString());
                throw;
            }
        });
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
        if (_titleBarConfigured)
        {
            ApplyProductIcon();
            return;
        }

        try
        {
            ConfigureCustomTitleBar();
            _titleBarConfigured = true;
        }
        catch (Exception ex)
        {
            CrashCaptureService.WriteCrash("titlebar-activate", ex, ex.ToString());
        }

        ApplyProductIcon();
    }

    private void BindWindowControls()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        ConfigureCustomTitleBar();
        EnsureClosingHook();
    }

    public void EnterBackgroundMode()
    {
        if (_appWindow is null)
        {
            BindWindowControls();
        }

        TrayIconService.Shared.EnsureInitialized();
        _appWindow?.Hide();
        TitleStatusText.Text = "백그라운드 실행 중 (트레이 아이콘에서 열기)";
    }

    public void RequestForceShutdown()
    {
        _forceShutdown = true;
    }

    public void ShowFromTray()
    {
        if (_appWindow is null)
        {
            BindWindowControls();
        }

        _appWindow?.Show();
        Activate();
        TitleStatusText.Text = StartupTrustStatusService.BuildTitleStatus();
        Services.Update.AutoUpdateCheckService.Shared.TryShowPendingPopup();
    }

    private void EnsureClosingHook()
    {
        if (_closingHooked || _appWindow is null)
        {
            return;
        }

        _closingHooked = true;
        _appWindow.Closing += (_, args) =>
        {
            if (_forceShutdown)
            {
                return;
            }

            var prefs = BackgroundRunPreferences.Load();
            if (prefs.RunInBackgroundOnClose)
            {
                args.Cancel = true;
                TrayIconService.Shared.EnsureInitialized();
                _appWindow?.Hide();
                TitleStatusText.Text = "백그라운드 실행 중 (트레이 아이콘에서 열기)";
            }
        };
    }

    private void BuildApplicationShell()
    {
        TitleBarIcon = new Image { Width = 22, Height = 22 };
        TitleStatusText = new TextBlock
        {
            Text = "준비됨",
            Margin = new Thickness(20, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 102, 112, 133))
        };
        VersionText = new TextBlock
        {
            Text = "버전 …",
            FontSize = 11,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 102, 112, 133))
        };
        SearchBox = new TextBox { PlaceholderText = "메뉴 검색" };
        SearchBox.TextChanged += SearchChanged;
        NavPanel = new StackPanel { Padding = new Thickness(14, 8, 14, 20), Spacing = 2 };
        RootFrame.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 244, 245, 247));

        var minimize = new Button { Content = "─", Width = 40, Height = 32 };
        minimize.Click += OnMinimizeWindow;
        var maximize = new Button { Content = "□", Width = 40, Height = 32 };
        maximize.Click += OnMaximizeWindow;
        var close = new Button { Content = "✕", Width = 40, Height = 32 };
        close.Click += OnCloseWindow;

        AppTitleBar = new Grid
        {
            Height = 38,
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 221, 226, 234)),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(48, 31, 41, 55)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(272) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) }
            }
        };

        var titleStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(20, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                TitleBarIcon,
                new TextBlock
                {
                    Text = "PC 케어 프로",
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 17, 24, 39))
                }
            }
        };
        Grid.SetColumn(titleStack, 0);
        AppTitleBar.Children.Add(titleStack);

        var statusHost = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 244, 245, 247)),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(48, 31, 41, 55)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = TitleStatusText
        };
        Grid.SetColumn(statusHost, 1);
        AppTitleBar.Children.Add(statusHost);

        var buttonsHost = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 244, 245, 247)),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(48, 31, 41, 55)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 8, 0),
                Children = { minimize, maximize, close }
            }
        };
        Grid.SetColumn(buttonsHost, 2);
        AppTitleBar.Children.Add(buttonsHost);

        var sidebarGrid = new Grid();
        sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sidebarGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var sidebarHeader = new StackPanel
        {
            Padding = new Thickness(20, 14, 20, 10),
            Spacing = 12,
            Children = { VersionText, SearchBox }
        };
        Grid.SetRow(sidebarHeader, 0);
        sidebarGrid.Children.Add(sidebarHeader);

        var sidebarSeparator = new Rectangle
        {
            Height = 1,
            Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(48, 31, 41, 55))
        };
        Grid.SetRow(sidebarSeparator, 1);
        sidebarGrid.Children.Add(sidebarSeparator);

        var sidebarScroll = new ScrollViewer { Content = NavPanel };
        Grid.SetRow(sidebarScroll, 2);
        sidebarGrid.Children.Add(sidebarScroll);

        var sidebar = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 221, 226, 234)),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(48, 31, 41, 55)),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = sidebarGrid
        };

        var body = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(272) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            Children = { sidebar, RootFrame }
        };
        Grid.SetColumn(RootFrame, 1);

        Content = new Grid
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 244, 245, 247)),
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(38) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Children = { AppTitleBar, body }
        };
        Grid.SetRow(body, 1);
    }

    private void OnCloseWindow(object sender, RoutedEventArgs e)
    {
        var prefs = BackgroundRunPreferences.Load();
        if (prefs.RunInBackgroundOnClose)
        {
            EnterBackgroundMode();
            return;
        }

        _forceShutdown = true;
        Close();
    }

    private void OnMinimizeWindow(object sender, RoutedEventArgs e)
    {
        if (_appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.Minimize();
        }
    }

    private void OnMaximizeWindow(object sender, RoutedEventArgs e)
    {
        if (_appWindow?.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        if (presenter.State == OverlappedPresenterState.Maximized)
        {
            presenter.Restore();
        }
        else
        {
            presenter.Maximize();
        }
    }

    private void BuildDefaultNavigation()
    {
        if (NavPanel.Children.Count > 0)
        {
            return;
        }

        void AddSection(string title, string tag)
        {
            var section = new TextBlock
            {
                Text = title,
                Tag = tag
            };
            section.Style = (Style)Application.Current.Resources["MacSectionTitleStyle"];
            NavPanel.Children.Add(section);
        }

        void AddNav(string title, string symbol, string tag, RoutedEventHandler click)
        {
            var button = new Controls.MacNavigationButton
            {
                Title = title,
                Symbol = symbol,
                Tag = tag
            };
            button.Click += click;
            NavPanel.Children.Add(button);
        }

        AddSection("주요 기능", "section:primary");
        AddNav("홈", "◫", "nav:home", OpenDashboard);
        AddNav("통합 점검", "⌁", "nav:pc-check", OpenUnifiedCare);
        AddNav("시스템 케어", "◉", "nav:system-care", OpenSystemCare);
        AddNav("보안 금고", "⬢", "nav:secure-vault", OpenSecureVault);
        AddNav("보안 삭제", "⊗", "nav:secure-delete", OpenSecureDelete);

        AddSection("기록", "section:history");
        AddNav("보고서", "□", "nav:reports", OpenReports);
        AddNav("작업 내역", "◇", "nav:activity", OpenActivity);

        AddSection("설정", "section:settings");
        AddNav("환경설정", "⚙", "nav:settings", OpenSettings);
        AddNav("기능 관리", "☷", "nav:feature-mgmt", OpenFeatureManagement);
        AddNav("고급 도구", "◈", "nav:advanced-center", OpenAdvancedCenter);
    }

    private void ApplyProductIcon()
    {
        if (_appWindow is null)
        {
            return;
        }

        var iconPath = ProductIconService.ResolveIconPath();
        if (string.IsNullOrWhiteSpace(iconPath))
        {
            return;
        }

        try
        {
            TitleBarIcon.Source = new BitmapImage(new Uri(iconPath));
            _appWindow?.SetIcon(iconPath);
        }
        catch
        {
            // Icon is cosmetic; startup must continue.
        }
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