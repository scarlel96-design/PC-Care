using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Services.Commercial;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class DevicePurgeCenterPage : Page
{
    private readonly StorageSanitizerDryRun _sanitizer = new();
    private readonly StoragePurgeReadinessService _readinessService = new();
    private readonly StoragePurgePreExecutionGate _gate = new();
    private readonly StoragePurgeAuthorizationService _authorizationService = new();
    private PurgeSnapshot? _snapshot;
    private PurgeAuthorizationChallenge? _authorizationChallenge;
    private bool _analysisInProgress;
    private bool _executionInProgress;

    public DevicePurgeCenterPage()
    {
        InitializeComponent();
        var roots = DriveInfo.GetDrives()
            .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
            .Select(drive => drive.RootDirectory.FullName)
            .OrderBy(root => root, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        DriveBox.ItemsSource = roots;
        if (roots.Length > 0) DriveBox.SelectedIndex = 0;
        AnalysisStatusText.Text = roots.Length == 0 ? "분석 가능한 고정 로컬 드라이브를 찾지 못했습니다." : "대기 · 분석은 읽기 전용으로 수행됩니다.";
    }

    private async void Analyze(object sender, RoutedEventArgs e)
    {
        if (_analysisInProgress) return;
        if (DriveBox.SelectedItem is not string root)
        {
            AnalysisStatusText.Text = "분석할 로컬 드라이브를 선택하세요.";
            return;
        }

        _analysisInProgress = true;
        AnalyzeButton.IsEnabled = false;
        AttestButton.IsEnabled = false;
        AnalysisStatusText.Text = "물리 디스크와 NVMe 읽기 전용 증적을 분석 중…";
        try
        {
            _snapshot = await Task.Run(() => Collect(root));
            DeviceSummaryText.Text = _snapshot.DeviceSummary;
            MethodSummaryText.Text = _snapshot.MethodSummary;
            AnalysisStatusText.Text = _snapshot.Assessment.IsEligibleForFutureExecutor
                ? "비파괴 사전 검증을 통과했습니다. WinPE에서 일회용 승인과 최종 확인을 거친 경우에만 실행할 수 있습니다."
                : "분석 완료 · 미래 실행기 후보는 안전 규칙에 따라 차단되었습니다.";
            AttestButton.IsEnabled = true;
            _authorizationChallenge = null;
            AuthorizationInput.Text = "";
            ExecutionConfirmBox.Text = "";
            ExecutePurgeButton.IsEnabled = false;
            ExecutionProgressBar.Value = 0;
            WinPeExecutionPanel.Visibility = _snapshot.Plan.IsWinPe ? Visibility.Visible : Visibility.Collapsed;
            AuthorizationText.Text = _snapshot.Plan.IsWinPe
                ? "WinPE를 감지했습니다. 일회용 승인을 시작한 뒤 표시되는 문구를 정확히 입력하세요."
                : "일반 Windows에서는 장치 PURGE 실행 UI가 비활성화됩니다.";
        }
        catch (Exception exception)
        {
            _snapshot = null;
            AnalysisStatusText.Text = "분석 실패: " + exception.Message;
        }
        finally
        {
            _analysisInProgress = false;
            AnalyzeButton.IsEnabled = true;
        }
    }

    private PurgeSnapshot Collect(string root)
    {
        var plan = _sanitizer.PlanDryRun(root);
        var inspection = new WindowsStoragePurgeReadOnlyInspector().Inspect(plan);
        NvmeIdentifyReadOnlyEvidence? identify = null;
        NvmeCommandEffectsReadOnlyEvidence? effects = null;
        NvmeSanitizeStatusReadOnlyEvidence? status = null;
        if (string.Equals(plan.Device.BusType, "NVMe", StringComparison.OrdinalIgnoreCase))
        {
            identify = new WindowsNvmeIdentifyReadOnlyReader().Read(plan);
            effects = new WindowsNvmeCommandEffectsReadOnlyReader().Read(plan);
            status = new WindowsNvmeSanitizeStatusReadOnlyReader().Read(plan);
        }

        var readiness = _readinessService.Evaluate(plan, inspection, identify);
        var authorization = new PurgeAuthorizationDecision
        {
            AuthorizedForFutureExecutor = false,
            ExecutionEnabled = false,
            DeviceFingerprint = DeviceFingerprint.Create(plan.Device),
            Message = "읽기 전용 분석에는 실행 승인 토큰을 만들지 않습니다."
        };
        var assessment = _gate.Assess(plan, authorization, inspection, identify, effects, status, DateTimeOffset.UtcNow);
        return new PurgeSnapshot(plan, authorization, inspection, identify, effects, status, assessment,
            BuildDeviceSummary(plan, inspection, identify, effects, status, assessment), BuildMethodSummary(readiness, assessment));
    }

    private void CreateAttestation(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null)
        {
            AttestationText.Text = "먼저 지원 상태 분석을 실행하세요.";
            return;
        }
        try
        {
            var receipt = new StoragePurgeAttestationBundleService().Write(
                _snapshot.Plan, _snapshot.Authorization, _snapshot.Inspection, _snapshot.Identify, _snapshot.Effects, _snapshot.Status, _snapshot.Assessment);
            AttestationText.Text = receipt.IsValid
                ? "증적 저장 완료 · " + receipt.Path + "\n실행 권한: 없음"
                : "증적 파일을 만들었지만 즉시 검증에 실패했습니다. 파일을 사용하지 마세요.";
        }
        catch (Exception exception)
        {
            AttestationText.Text = "증적 저장 실패: " + exception.Message;
        }
    }

    private void AuthorizeForExecution(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null || !_snapshot.Plan.IsWinPe || _executionInProgress) return;
        try
        {
            if (_authorizationChallenge is null)
            {
                _authorizationChallenge = _authorizationService.Begin(_snapshot.Plan);
                AuthorizationInput.Text = "";
                AuthorizationText.Text = "다음 문구를 정확히 입력하세요:\n" + _authorizationChallenge.RequiredPhrase;
                AuthorizeButton.Content = "일회용 승인 확인";
                return;
            }

            var approval = _authorizationService.Confirm(_snapshot.Plan, _authorizationChallenge.OperationId, AuthorizationInput.Text ?? "");
            var assessment = _gate.Assess(_snapshot.Plan, approval, _snapshot.Inspection, _snapshot.Identify, _snapshot.Effects, _snapshot.Status, DateTimeOffset.UtcNow);
            _snapshot = _snapshot with
            {
                Authorization = approval,
                Assessment = assessment,
                DeviceSummary = BuildDeviceSummary(_snapshot.Plan, _snapshot.Inspection, _snapshot.Identify, _snapshot.Effects, _snapshot.Status, assessment),
                MethodSummary = BuildMethodSummary(_readinessService.Evaluate(_snapshot.Plan, _snapshot.Inspection, _snapshot.Identify), assessment)
            };
            DeviceSummaryText.Text = _snapshot.DeviceSummary;
            MethodSummaryText.Text = _snapshot.MethodSummary;
            _authorizationChallenge = null;
            AuthorizeButton.Content = "일회용 승인 시작";
            ExecutePurgeButton.IsEnabled = approval.AuthorizedForFutureExecutor && assessment.IsEligibleForFutureExecutor;
            var method = SelectedMethod();
            AuthorizationText.Text = approval.Message + "\n\n최종 실행 문구:\n" + WinPeStoragePurgeExecutor.RequiredConfirmation(method, _snapshot.Plan.Device.DiskNumber);
            ExecutionConfirmBox.Text = "";
        }
        catch (Exception exception)
        {
            _authorizationChallenge = null;
            AuthorizeButton.Content = "일회용 승인 시작";
            AuthorizationText.Text = "승인 처리 실패: " + exception.Message;
            ExecutePurgeButton.IsEnabled = false;
        }
    }

    private async void ExecuteWinPePurge(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null || _executionInProgress || !_snapshot.Assessment.IsEligibleForFutureExecutor) return;
        _executionInProgress = true;
        ExecutePurgeButton.IsEnabled = false;
        AnalyzeButton.IsEnabled = false;
        try
        {
            var method = SelectedMethod();
            var progress = new Progress<WinPePurgeProgress>(item =>
            {
                ExecutionProgressBar.Value = item.Percent;
                ExecutionStatusText.Text = item.Detail;
            });
            var result = await new WinPeStoragePurgeExecutor().ExecuteAsync(new WinPePurgeExecutionRequest
            {
                Plan = _snapshot.Plan,
                Authorization = _snapshot.Authorization,
                Assessment = _snapshot.Assessment,
                Method = method,
                ConfirmationPhrase = ExecutionConfirmBox.Text ?? "",
                Timeout = TimeSpan.FromHours(2)
            }, progress);
            ExecutionProgressBar.Value = result.Completed ? 100 : ExecutionProgressBar.Value;
            ExecutionStatusText.Text = result.Message + (result.EvidenceValid && !string.IsNullOrWhiteSpace(result.EvidencePath)
                ? Environment.NewLine + "실행 증적 검증 완료 · " + result.EvidencePath
                : Environment.NewLine + "실행 증적을 검증하지 못했습니다. 장치를 재사용하지 말고 수동 점검하세요.");
        }
        catch (Exception exception)
        {
            ExecutionStatusText.Text = "장치 PURGE 실행 거부 또는 실패: " + exception.Message;
        }
        finally
        {
            _executionInProgress = false;
            AnalyzeButton.IsEnabled = true;
            ExecutePurgeButton.IsEnabled = _snapshot?.Assessment.IsEligibleForFutureExecutor == true;
        }
    }

    private StorageSanitizeMethod SelectedMethod() =>
        ExecutionMethodBox.SelectedItem is ComboBoxItem item && Enum.TryParse<StorageSanitizeMethod>(item.Tag?.ToString(), out var method)
            ? method : StorageSanitizeMethod.CryptoErase;
    private static string BuildDeviceSummary(StorageSanitizeDryRunResult plan, PurgeReadOnlyInspection inspection, NvmeIdentifyReadOnlyEvidence? identify, NvmeCommandEffectsReadOnlyEvidence? effects, NvmeSanitizeStatusReadOnlyEvidence? status, PurgePreExecutionAssessment assessment) =>
        $"대상: {plan.Device.DisplayId}\n" +
        $"물리 재식별: {inspection.State} · {inspection.Detail}\n" +
        $"NVMe Identify: {(identify?.ReadSucceeded == true ? "수집됨" : identify?.Detail ?? "해당 없음")}\n" +
        $"Command Effects: {(effects?.ReadSucceeded == true ? (effects.SanitizeCommandSupported ? "Sanitize 지원 확인" : "Sanitize 미지원") : effects?.Detail ?? "해당 없음")}\n" +
        $"Sanitize Status: {(status?.ReadSucceeded == true ? status.State.ToString() : status?.Detail ?? "해당 없음")}\n" +
        $"실행: " + (assessment.IsEligibleForFutureExecutor ? "WinPE 최종 승인 대기" : "차단") + $" · 차단 사유 {assessment.BlockingReasons.Count}개";

    private static string BuildMethodSummary(StoragePurgeReadinessReport readiness, PurgePreExecutionAssessment assessment) =>
        string.Join("\n", readiness.Methods.Select(method => $"{method.Method}: {method.State} · {method.Detail}")) +
        "\n\n통합 게이트: " + assessment.Message;

    private sealed record PurgeSnapshot(
        StorageSanitizeDryRunResult Plan,
        PurgeAuthorizationDecision Authorization,
        PurgeReadOnlyInspection Inspection,
        NvmeIdentifyReadOnlyEvidence? Identify,
        NvmeCommandEffectsReadOnlyEvidence? Effects,
        NvmeSanitizeStatusReadOnlyEvidence? Status,
        PurgePreExecutionAssessment Assessment,
        string DeviceSummary,
        string MethodSummary);
}