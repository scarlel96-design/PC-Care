using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class AdvancedCenterPage : Page
{
    private readonly UserModeService _userMode = new();

    public AdvancedCenterPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootPanel.Children.Clear();
        RootPanel.Children.Add(new TextBlock { Text = "고급 도구", Style = Application.Current.Resources["MacPageTitleStyle"] as Style });
        RootPanel.Children.Add(new TextBlock
        {
            Text = "복구 방법, 규칙 데이터 등 전문 도구는 여기서만 열 수 있습니다.",
            Style = Application.Current.Resources["MacInlineStatusTextStyle"] as Style
        });

        if (!_userMode.Meets(UserMode.Advanced))
        {
            RootPanel.Children.Add(new TextBlock
            {
                Text = "현재 Basic 모드입니다. 아래 도구는 바로 열 수 있으며, 사이드바 고급 메뉴를 쓰려면 환경설정에서 Advanced 모드로 전환하세요.",
                Style = Application.Current.Resources["MacInlineStatusTextStyle"] as Style,
                TextWrapping = TextWrapping.Wrap
            });
        }

        AddSection("고급 도구", [
            ("복구 방법", "ProtocolCenterPage"),
            ("자세한 점검", "DeepScanSetupPage"),
            ("규칙 데이터", "KnowledgePackManagerPage"),
            ("프로그램 상태", "AppDiagnosticsPage"),
        ]);

        AddSection("상태·로그", [
            ("진행 상황", "ProgressHudPage"),
            ("업데이트", "UpdateStatusPage"),
            ("실행 로그", "StableLogLayoutPage"),
            ("오류 로그", "CrashLogPage"),
            ("복구 감사 로그", "RepairLogPage"),
            ("문제 분석", "IntelligenceCenterPage"),
        ]);

        if (_userMode.Meets(UserMode.Developer))
        {
            AddSection("개발자", [
                ("릴리즈 게이트", "ReleaseArtifactGatePage"),
                ("Final Lock", "FinalLockPage"),
                ("배포 준비 상태", "ReleaseStatusPage"),
                ("복구 품질 점검", "RepairHelperE2EGatePage"),
                ("안전 복구 마법사", "VerifiedRepairPage"),
                ("위험 작업 승인", "RiskGatePage"),
                ("자동 복구", "SelfHealingPage"),
                ("문제 보고서 만들기", "ErrorBundlePage"),
            ]);
        }
    }

    private void AddSection(string title, IEnumerable<(string label, string target)> items)
    {
        var visible = items.ToList();
        if (visible.Count == 0)
        {
            return;
        }

        RootPanel.Children.Add(new TextBlock
        {
            Text = title,
            Style = Application.Current.Resources["MacSectionHeaderStyle"] as Style
        });

        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        for (var i = 0; i < visible.Count; i++)
        {
            var (label, target) = visible[i];
            var button = new Button
            {
                Content = label,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Style = Application.Current.Resources["MacToolbarButtonStyle"] as Style,
                Tag = target
            };
            button.Click += (_, _) =>
            {
                var frame = App.Shell?.NavigationFrame;
                if (frame is not null)
                {
                    AppNavigationService.TryNavigateByName(frame, target);
                }
            };
            Grid.SetColumn(button, i % 2);
            Grid.SetRow(button, i / 2);
            grid.Children.Add(button);
        }

        grid.RowDefinitions.Add(new RowDefinition());
        if (visible.Count > 2)
        {
            grid.RowDefinitions.Add(new RowDefinition());
        }

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        RootPanel.Children.Add(grid);
    }
}