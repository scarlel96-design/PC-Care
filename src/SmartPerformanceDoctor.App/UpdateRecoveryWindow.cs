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
            var package=new TextBox {Header="이전 버전의 서명된 패키지 경로"};
            var previous=new TextBox {Header="패키지의 이전 버전",PlaceholderText="예: 51.0.3"};
            var consent=new CheckBox {Content="확인한 설치 파일을 이전 버전으로 복원하는 데 동의합니다."};
            var review=new Button {Content="구형 거래의 복원 계획 검사"};
            var execute=new Button {Content="검증된 관리자 복원 요청",IsEnabled=false};
            AutomationProperties.SetAutomationId(review,"LegacyRecoveryReview");
            AutomationProperties.SetAutomationId(execute,"LegacyRecoveryExecute");
            LegacyRecoveryPlan? plan=null;
            review.Click+=async(_,_)=>
            {
                execute.IsEnabled=false;review.IsEnabled=false;plan=null;
                try
                {
                    var path=package.Text;var version=previous.Text;
                    plan=await Task.Run(()=>UpdateRecoveryCoordinator.ReviewLegacy(path,version));
                    _status.Text=$"복원 계획: {plan.Status} · {plan.Targets.Count}개. 관리자 도구에서 대상 목록을 다시 확인하고 승인합니다.\n"+string.Join("\n",plan.Targets.Select(t=>$"{t.Path} · {t.OriginalLength} bytes · {t.CurrentContentState}"));
                    execute.IsEnabled=plan.Targets.Count>0&&plan.Targets.All(t=>t.Status=="BackupVerified"&&t.CurrentContentState is "OriginalVerified" or "UpdatedVerified")&&UpdateRecoveryCoordinator.CanRequestLegacyRecovery();
                    if(!execute.IsEnabled)_status.Text+=" 신뢰된 전환 도구 또는 복원 증거의 추가 확인이 필요합니다.";
                }
                catch(Exception){_status.Text="인증된 복원 계획을 확인하지 못했습니다. 원본 설치와 기록을 보존했습니다.";}
                finally{review.IsEnabled=true;}
            };
            execute.Click+=(_,_)=>
            {
                if(plan is null||consent.IsChecked!=true){_status.Text="복원 계획과 명시적 동의를 먼저 확인하세요.";return;}
                execute.IsEnabled=false;
                var result=UpdateRecoveryCoordinator.LaunchLegacy(plan,package.Text,previous.Text,true);
                _status.Text=result.Message;
                if(result.ProcessLaunched)Application.Current.Exit();
            };
            foreach(var control in new FrameworkElement[]{package,previous,consent,review,execute})panel.Children.Add(control);

        }
        var requestId=new TextBox{Header="승인 요청 ID",PlaceholderText="32자리 요청 ID"};
        AutomationProperties.SetAutomationId(requestId,"LegacyApprovalRequestId");
        var query=new Button{Content="승인·복원 결과 조회 (읽기 전용)"};
        AutomationProperties.SetAutomationId(query,"LegacyApprovalQuery");
        query.Click+=async(_,_)=>{query.IsEnabled=false;try{var id=requestId.Text;var observation=await Task.Run(()=>UpdateRecoveryCoordinator.InspectApproval(id));_status.Text=UpdateStateText.Approval(observation);}
            catch(Exception){_status.Text="승인 결과를 확인하지 못했습니다. 기록과 파일을 보존하며 추가 확인이 필요합니다.";}finally{query.IsEnabled=true;}};
        panel.Children.Add(requestId);panel.Children.Add(query);
        var close = new Button { Content = "닫기" };
        AutomationProperties.SetAutomationId(close, "UpdateRecoveryClose");
        close.Click += (_, _) => { Close(); Application.Current.Exit(); };
        panel.Children.Add(close);
        Content = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Show(status);
    }

    private void Show(UpdateRecoveryStatus status) => _status.Text = status.Code.StartsWith("INSTALLATION-",StringComparison.Ordinal)
        ? UpdateStateText.Installation(status.Code) : status.Code == "SIGNED-INSTALLATION-VERIFIED"
        ? "서명된 설치 목록과 실제 파일 해시를 확인했습니다." : status.State switch
    {
        UpdateRecoveryState.NoPendingTransaction => "미완료 업데이트가 없습니다. 전체 설치 신뢰 상태는 별도로 확인합니다.",
        UpdateRecoveryState.RecoveryCompleted => "이전 설치 상태의 복구가 완료됐습니다. 앱을 다시 실행하세요.",
        UpdateRecoveryState.RecoveryInProgress => "업데이트 복구가 진행 중입니다.",
        UpdateRecoveryState.RecoveryAvailable => "미완료 업데이트가 발견됐습니다. 복구 결과 확인이 필요합니다.",
        _ => "자동 복구를 확인하지 못했습니다. 원본 기록을 보존하며 추가 확인이 필요합니다."
    };
}
