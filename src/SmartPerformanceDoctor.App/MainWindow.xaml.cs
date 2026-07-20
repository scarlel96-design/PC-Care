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
using SmartPerformanceDoctor.App.Platform;
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
    private ProductCatalog _productCatalog = null!;

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
        _productCatalog = ProductComposition.Current;
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
                SelectNavigation("home");
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

    public void SetStatusMessage(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            TitleStatusText.Text = message;
        }
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
        var canvasBrush = GetBrush("PccCanvasBrush", 255, 243, 246, 251);
        var sidebarBrush = GetBrush("PccSidebarBrush", 255, 233, 238, 246);
        var textBrush = GetBrush("PccTextBrush", 255, 23, 35, 60);
        var mutedBrush = GetBrush("PccTextMutedBrush", 255, 113, 128, 150);
        var borderBrush = GetBrush("PccBorderBrush", 26, 100, 116, 139);
        TitleBarIcon = new Image { Width = 22, Height = 22 };
        TitleStatusText = new TextBlock
        {
            Text = "준비됨",
            Margin = new Thickness(20, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Foreground = mutedBrush
        };
        VersionText = new TextBlock
        {
            Text = "버전 …",
            FontSize = 11,
            Foreground = mutedBrush
        };
        SearchBox = new TextBox { PlaceholderText = "메뉴 검색" };
        SearchBox.TextChanged += SearchChanged;
        NavPanel = new StackPanel { Padding = new Thickness(14, 8, 14, 20), Spacing = 2 };
        RootFrame.Background = canvasBrush;

        var minimize = new Button { Content = "─", Width = 40, Height = 32 };
        minimize.Click += OnMinimizeWindow;
        var maximize = new Button { Content = "□", Width = 40, Height = 32 };
        maximize.Click += OnMaximizeWindow;
        var close = new Button { Content = "✕", Width = 40, Height = 32 };
        close.Click += OnCloseWindow;

        AppTitleBar = new Grid
        {
            Height = 44,
            Background = sidebarBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(244) },
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
                    Text = "PCCare",
                    FontSize = 14,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = textBrush
                }
            }
        };
        Grid.SetColumn(titleStack, 0);
        AppTitleBar.Children.Add(titleStack);

        var statusHost = new Border
        {
            Background = canvasBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = TitleStatusText
        };
        Grid.SetColumn(statusHost, 1);
        AppTitleBar.Children.Add(statusHost);

        var buttonsHost = new Border
        {
            Background = canvasBrush,
            BorderBrush = borderBrush,
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
            Fill = borderBrush
        };
        Grid.SetRow(sidebarSeparator, 1);
        sidebarGrid.Children.Add(sidebarSeparator);

        var sidebarScroll = new ScrollViewer { Content = NavPanel };
        Grid.SetRow(sidebarScroll, 2);
        sidebarGrid.Children.Add(sidebarScroll);

        var sidebar = new Border
        {
            Background = sidebarBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = sidebarGrid
        };

        var body = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(244) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
            },
            Children = { sidebar, RootFrame }
        };
        Grid.SetColumn(RootFrame, 1);

        Content = new Grid
        {
            Background = canvasBrush,
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(44) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Children = { AppTitleBar, body }
        };
        Grid.SetRow(body, 1);
    }

    private static Microsoft.UI.Xaml.Media.SolidColorBrush GetBrush(
        string resourceKey,
        byte alpha,
        byte red,
        byte green,
        byte blue)
    {
        return Application.Current.Resources.TryGetValue(resourceKey, out var resource) &&
               resource is Microsoft.UI.Xaml.Media.SolidColorBrush brush
            ? brush
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(ColorHelper.FromArgb(alpha, red, green, blue));
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

        void AddNav(ProductFeatureDescriptor feature)
        {
            var button = new Controls.MacNavigationButton
            {
                Title = feature.Title,
                Symbol = feature.Glyph,
                Tag = feature.Id
            };
            button.Click += (_, _) => NavigateFeature(feature);
            NavPanel.Children.Add(button);
        }

        var primary = _productCatalog.Features
            .Where(feature => feature.IsPrimaryNavigation && feature.Area != ProductArea.Settings)
            .OrderBy(feature => feature.Order)
            .ToArray();
        var settings = _productCatalog.Features
            .Where(feature => feature.IsPrimaryNavigation && feature.Area == ProductArea.Settings)
            .OrderBy(feature => feature.Order)
            .ToArray();

        AddSection("관리", "section:primary");
        foreach (var feature in primary)
        {
            AddNav(feature);
        }

        AddSection("프로그램", "section:settings");
        foreach (var feature in settings)
        {
            AddNav(feature);
        }
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

    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        var query = (SearchBox.Text ?? string.Empty).Trim();
        var hasQuery = query.Length > 0;

        foreach (var button in EnumerateNavButtons())
        {
            var featureVisible = IsNavVisible(button);
            var searchVisible = !hasQuery || MatchesSearch(button, query);
            button.Visibility = featureVisible && searchVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        UpdateSectionVisibility();
    }

    private bool IsNavVisible(MacNavigationButton button)
    {
        return button.Tag is string id &&
               _productCatalog.TryGetFeature(id, out var feature) &&
               _userMode.Meets(feature.MinimumMode) &&
               NavigationFeatureMap.ShouldShow(feature.InstallFeatureId, _installedFeatures);
    }

    private bool MatchesSearch(MacNavigationButton button, string query)
    {
        if (button.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return button.Tag is string id &&
               _productCatalog.TryGetFeature(id, out var feature) &&
               (feature.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                feature.Keywords.Any(keyword => keyword.Contains(query, StringComparison.OrdinalIgnoreCase)));
    }

    private void SelectNavigation(string featureId)
    {
        foreach (var button in EnumerateNavButtons())
        {
            button.IsSelected = string.Equals(button.Tag as string, featureId, StringComparison.OrdinalIgnoreCase);
        }
    }
    private void NavigateFeature(ProductFeatureDescriptor feature)
    {
        try
        {
            AppNavigationService.Navigate(RootFrame, feature.PageType);
            SelectNavigation(feature.Id);
            TitleStatusText.Text = StartupTrustStatusService.BuildTitleStatus();
        }
        catch (Exception ex)
        {
            ShowNavFeedback($"{feature.Title} 오류: {ex.Message}");
        }
    }

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

}
