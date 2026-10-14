using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation;
using SmartPerformanceDoctor.App.Services.Update;
using SmartPerformanceDoctor.UpdateEngine;

namespace SmartPerformanceDoctor.App;

/// <summary>Keep a pending installation out of the normal product shell.</summary>
public sealed class UpdateRecoveryWindow : Window
{
    private readonly TextBlock _status;
    public UpdateRecoveryWindow(UpdateRecoveryStatus status, bool readOnly)
    {
        Title = "PC 케어 · 업데이트 상태";
        var panel = new StackPanel { Padding = new Thickness(28), Spacing = 18, MaxWidth = 680,
            HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.Children.Add(new TextBlock { Text = "업데이트 상태 확인", FontSize = 28, TextWrapping = TextWrapping.Wrap });
        _status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 16 };
        AutomationProperties.SetAutomationId(_status, "UpdateRecoveryStatus");
        panel.Children.Add(_status);
        panel.Children.Add(new TextBlock { Text = "미완료 업데이트가 있으면 일반 기능을 열기 전에 복구 결과를 확인합니다.",
            TextWrapping = TextWrapping.Wrap });
        var refresh = new Button { Content = "상태 다시 확인" };
        AutomationProperties.SetAutomationId(refresh, "UpdateRecoveryRefresh");
        refresh.Click += (_, _) => Show(UpdateRecoveryCoordinator.Inspect());
        panel.Children.Add(refresh);
        if (!readOnly)
        {
            var recover = new Button { Content = "관리자 복구 요청" };
            AutomationProperties.SetAutomationId(recover, "UpdateRecoveryRequest");
            recover.Click += (_, _) =>
            {
                var result = UpdateRecoveryCoordinator.LaunchElevatedRecovery();
                _status.Text = result == RecoveryLaunchResult.Cancelled ? "관리자 승인이 취소됐습니다. 기존 상태를 보존했습니다." :
                    result == RecoveryLaunchResult.Launched ? "복구 도우미를 시작했습니다. 앱을 종료한 뒤 다시 실행해 결과를 확인하세요." :
                    "신뢰할 수 있는 복구 도우미를 시작하지 못했습니다. 설치 자료와 거래 기록을 보존했습니다.";
                if (result == RecoveryLaunchResult.Launched) Application.Current.Exit();
            };
            panel.Children.Add(recover);
        }
        var close = new Button { Content = "닫기" };
        AutomationProperties.SetAutomationId(close, "UpdateRecoveryClose");
        close.Click += (_, _) => { Close(); Application.Current.Exit(); };
        panel.Children.Add(close);
        Content = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Show(status);
    }

    private void Show(UpdateRecoveryStatus status) => _status.Text = status.State switch
    {
        UpdateRecoveryState.NoPendingTransaction => "미완료 업데이트가 없습니다.",
        UpdateRecoveryState.RecoveryCompleted => "이전 설치 상태의 복구가 완료됐습니다. 앱을 다시 실행하세요.",
        UpdateRecoveryState.RecoveryInProgress => "업데이트 복구가 진행 중입니다.",
        UpdateRecoveryState.RecoveryAvailable => "미완료 업데이트가 발견됐습니다. 복구 결과 확인이 필요합니다.",
        _ => "자동 복구를 확인하지 못했습니다. 원본 기록을 보존하며 추가 확인이 필요합니다."
    };
}
