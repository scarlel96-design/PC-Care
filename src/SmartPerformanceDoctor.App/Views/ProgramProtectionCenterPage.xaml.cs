using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SmartPerformanceDoctor.App.Branding;
using SmartPerformanceDoctor.Aegis;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.App.Services.Aegis;
using SmartPerformanceDoctor.App.Services.Commercial;
using Windows.UI;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class ProgramProtectionCenterPage : Page
{
    private static readonly SolidColorBrush OkBrush = new(Color.FromArgb(255, 232, 245, 233));
    private static readonly SolidColorBrush WarnBrush = new(Color.FromArgb(255, 255, 243, 224));
    private static readonly SolidColorBrush ErrorBrush = new(Color.FromArgb(255, 255, 235, 238));
    private static readonly SolidColorBrush OkBorder = new(Color.FromArgb(255, 165, 214, 167));
    private static readonly SolidColorBrush WarnBorder = new(Color.FromArgb(255, 255, 204, 128));
    private static readonly SolidColorBrush ErrorBorder = new(Color.FromArgb(255, 239, 154, 154));

    private readonly ProgramProtectionService _service = new();
    private bool _refreshInProgress;
    private CancellationTokenSource? _refreshCts;

    public ProgramProtectionCenterPage()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"프로그램 보호 화면을 불러오지 못했습니다: {ex.Message}", ex);
        }

        AegisDisclaimerText.Text = AstraCareBranding.AegisDisclaimer;
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
        AegisProtectionBackgroundService.Shared.StatusChanged += OnBackgroundStatusChanged;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnPageUnloaded;
        AegisProtectionBackgroundService.Shared.StatusChanged -= OnBackgroundStatusChanged;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnPageLoaded;
        UpdateAutoProtectionBanner();
        await RefreshAsync();
    }

    private void OnBackgroundStatusChanged()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateAutoProtectionBanner();
            var status = AegisProtectionBackgroundService.Shared.LastStatus;
            if (status is not null && !_refreshInProgress)
            {
                ApplyMirrorStatus(status);
            }
        });
    }

    private async void Verify(object sender, RoutedEventArgs e) => await RefreshAsync(attemptRepair: true);

    private async void RebuildBaseline(object sender, RoutedEventArgs e)
    {
        if (_refreshInProgress)
        {
            return;
        }

        _refreshInProgress = true;
        try
        {
            MirrorStatusText.Text = "기준선 재생성 중…";
            await Task.Run(() => AegisMirrorService.Shared.RebuildBaseline(AppInfo.BuildVersion));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MirrorStatusText.Text = "기준선 재생성 실패";
            MirrorDetailText.Text = FormatError(ex);
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private async void ExportOfflineCapsule(object sender, RoutedEventArgs e)
    {
        if (_refreshInProgress)
        {
            return;
        }

        _refreshInProgress = true;
        try
        {
            MirrorStatusText.Text = "오프라인 캡슐 생성 중…";
            var path = await Task.Run(() => AegisMirrorService.Shared.ExportOfflineCapsule());
            MirrorStatusText.Text = "오프라인 복구 캡슐을 생성했습니다.";
            MirrorDetailText.Text = path;
        }
        catch (Exception ex)
        {
            MirrorStatusText.Text = "오프라인 캡슐 생성 실패";
            MirrorDetailText.Text = FormatError(ex);
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private async Task RefreshAsync(bool attemptRepair = false)
    {
        if (_refreshInProgress)
        {
            return;
        }

        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource();
        var token = _refreshCts.Token;

        _refreshInProgress = true;
        try
        {
            MirrorStatusText.Text = "복구 미러 상태 확인 중…";
            StatusText.Text = "설치 무결성 검사 중…";

            var mirror = AegisProtectionBackgroundService.Shared.LastStatus;
            if (mirror is null || attemptRepair)
            {
                mirror = await Task.Run(
                    () => AegisMirrorService.Shared.RunManualCheck(AppInfo.BuildVersion, attemptRepair),
                    token);
            }

            token.ThrowIfCancellationRequested();

            var report = await Task.Run(() => _service.VerifyInstallIntegrity(), token);
            token.ThrowIfCancellationRequested();

            ApplyMirrorStatus(mirror);
            UpdateAutoProtectionBanner();

            StatusText.Text = report.Message;
            ExeHashText.Text = string.IsNullOrWhiteSpace(report.ExeSha256)
                ? report.ExePath
                : $"{report.ExePath}\nSHA256: {report.ExeSha256}";

            var findings = new List<string>();
            if (AegisMirrorPaths.UsingUserFallback)
            {
                findings.Add($"[복구 미러] ProgramData 접근 불가 — 사용자 폴더 사용: {AegisMirrorPaths.Root}");
            }

            if (mirror.Findings.Count > 0)
            {
                findings.AddRange(mirror.Findings.Select(f => $"[복구 미러] {f}"));
            }

            if (report.Findings.Count > 0)
            {
                findings.AddRange(report.Findings);
            }

            FindingList.ItemsSource = findings.Count > 0
                ? findings
                : new[] { "복구 미러 · 핵심 실행 파일 · Knowledge Pack 정상 — 자동 감시 중" };
        }
        catch (OperationCanceledException)
        {
            // Page navigated away while refresh was running.
        }
        catch (Exception ex)
        {
            MirrorStatusText.Text = "프로그램 보호 화면을 불러오지 못했습니다.";
            MirrorDetailText.Text = FormatError(ex);
            StatusText.Text = "설치 무결성 검사를 완료하지 못했습니다.";
            ExeHashText.Text = "";
            FindingList.ItemsSource = new[] { FormatError(ex) };
        }
        finally
        {
            _refreshInProgress = false;
        }
    }

    private void ApplyMirrorStatus(AegisMirrorStatus mirror)
    {
        UpdateSafeModeWarning(mirror);
        MirrorStatusText.Text = mirror.Message;
        MirrorDetailText.Text = BuildMirrorDetail(mirror);
        ProtectionLevelValue.Text = mirror.ProtectionLevel.ToString();
        UpdateLevelSegments(mirror.ProtectionLevel);
        ProtectionLevelCaption.Text = DescribeProtectionLevel(mirror.ProtectionLevel);

        SetChip(ChipManifest, ChipManifestValue, mirror.ManifestSignatureValid ? "서명 정상" : "서명 오류", mirror.ManifestSignatureValid);
        SetChip(ChipCapsule, ChipCapsuleValue, mirror.CapsuleHashValid ? "해시 정상" : (mirror.CapsuleReady ? "해시 오류" : "없음"), mirror.CapsuleHashValid || !mirror.CapsuleReady);
        SetChip(ChipAudit, ChipAuditValue, mirror.AuditChainValid ? "정상" : "오류", mirror.AuditChainValid);
        SetChip(
            ChipService,
            ChipServiceValue,
            mirror.RecoveryServiceInstalled
                ? (mirror.RecoveryServiceRunning ? "실행 중" : "중지")
                : "미설치",
            mirror.RecoveryServiceInstalled && mirror.RecoveryServiceRunning);
        SetChip(ChipBackup, ChipBackupValue, mirror.BackupSlotReady ? "준비됨" : "없음", mirror.BackupSlotReady);
        SetChip(ChipOffline, ChipOfflineValue, mirror.OfflineCapsuleReady ? "준비됨" : "없음", mirror.OfflineCapsuleReady);
        SetChip(
            ChipTpm,
            ChipTpmValue,
            $"{mirror.KeyProtectionMode} · {(mirror.TpmAvailable ? "TPM" : "DPAPI")}",
            mirror.TpmAvailable);
        var signingReady = AegisSigningRuntime.IsSigningConfigured();
        SetChip(ChipSigning, ChipSigningValue, signingReady ? "구성됨" : "없음", signingReady);
        SetChip(
            ChipMirrorPath,
            ChipMirrorPathValue,
            AegisMirrorPaths.UsingUserFallback ? "사용자 폴더" : "ProgramData",
            !AegisMirrorPaths.UsingUserFallback);
    }

    private void UpdateAutoProtectionBanner()
    {
        var bg = AegisProtectionBackgroundService.Shared;
        if (bg.IsRunning)
        {
            AutoProtectionBanner.Background = OkBrush;
            AutoProtectionBanner.BorderBrush = OkBorder;
            AutoProtectionTitle.Text = "상시 자동 보호 활성";
            AutoProtectionTitle.Foreground = new SolidColorBrush(Color.FromArgb(255, 27, 94, 32));
            AutoProtectionDetail.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
            AutoProtectionIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
            AutoProtectionBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
            AutoProtectionBadge.Text = "AUTO";
            var last = bg.LastCycleAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "시작 직후";
            var next = bg.NextCycleAt?.ToLocalTime().ToString("HH:mm") ?? "—";
            AutoProtectionDetail.Text =
                $"{bg.LastMessage}\n" +
                $"주기 {AegisWatchdogRunner.DefaultInterval.TotalMinutes:0}분 · 마지막 주기 {last} · 다음 예정 {next}";
            return;
        }

        AutoProtectionBanner.Background = WarnBrush;
        AutoProtectionBanner.BorderBrush = WarnBorder;
        AutoProtectionTitle.Text = "자동 보호 준비 중";
        AutoProtectionTitle.Foreground = new SolidColorBrush(Color.FromArgb(255, 180, 83, 9));
        AutoProtectionDetail.Foreground = new SolidColorBrush(Color.FromArgb(255, 146, 64, 14));
        AutoProtectionIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 180, 83, 9));
        AutoProtectionBadgeBorder.Background = new SolidColorBrush(Color.FromArgb(255, 180, 83, 9));
        AutoProtectionBadge.Text = "WAIT";
        AutoProtectionDetail.Text = ProcessElevationService.IsAdministrator()
            ? "앱 시작 후 자동 보호 루프가 곧 활성화됩니다."
            : "관리자 권한으로 실행하면 ProgramData 복구 미러가 활성화됩니다.";
    }

    private void UpdateLevelSegments(int level)
    {
        var active = new SolidColorBrush(Color.FromArgb(255, 10, 132, 255));
        var inactive = new SolidColorBrush(Color.FromArgb(255, 220, 220, 220));
        var segments = new[] { LevelSeg1, LevelSeg2, LevelSeg3, LevelSeg4, LevelSeg5 };
        for (var i = 0; i < segments.Length; i++)
        {
            segments[i].Background = i < level ? active : inactive;
        }
    }

    private static void SetChip(Border chip, TextBlock valueBlock, string value, bool ok)
    {
        valueBlock.Text = value;
        chip.Background = ok ? OkBrush : (value.Contains("오류", StringComparison.Ordinal) ? ErrorBrush : WarnBrush);
        chip.BorderBrush = ok ? OkBorder : (value.Contains("오류", StringComparison.Ordinal) ? ErrorBorder : WarnBorder);
    }

    private static string DescribeProtectionLevel(int level) => level switch
    {
        5 => "최고 등급 — 매니페스트·캡슐·감사체인·복구 서비스·오프라인 팩·백업 슬롯이 모두 준비되었습니다.",
        4 => "강화 등급 — 매니페스트·캡슐·복구 서비스가 활성입니다.",
        3 => "표준 등급 — 매니페스트와 캡슐이 정상입니다.",
        2 => "기본 등급 — 매니페스트 서명은 정상이나 일부 구성요소가 비활성입니다.",
        _ => "제한 등급 — 서명·캡슐 검증에 문제가 있습니다. 자동 복구가 진행 중일 수 있습니다."
    };

    private static string BuildMirrorDetail(AegisMirrorStatus mirror)
    {
        var detail =
            $"미러 경로: {AegisMirrorPaths.Root}\n" +
            $"보호 파일 {mirror.ProtectedFileCount}개 · 매니페스트 {(mirror.ManifestReady ? "준비됨" : "없음")} ({mirror.ManifestSource}) · " +
            $"자동 복구 {mirror.RepairedFiles}건 · 마지막 검사 {mirror.LastCheckAt?.ToLocalTime():yyyy-MM-dd HH:mm}";

        if (mirror.LastRepairAt is not null)
        {
            detail += $" · 마지막 복구 {mirror.LastRepairAt.Value.ToLocalTime():yyyy-MM-dd HH:mm}";
        }

        if (!string.IsNullOrWhiteSpace(mirror.RecoveryReportPath))
        {
            detail += $"\n보고서: {mirror.RecoveryReportPath}";
        }

        return detail;
    }

    private static string FormatError(Exception ex)
    {
        if (ex is StackOverflowException)
        {
            return "내부 스택 오버플로가 발생했습니다. 앱을 다시 시작한 뒤 무결성 검사를 다시 시도하세요.";
        }

        var message = ex.GetBaseException().Message;
        return string.IsNullOrWhiteSpace(message) ? ex.GetType().Name : message;
    }

    private void UpdateSafeModeWarning(AegisMirrorStatus mirror)
    {
        var active = (mirror.SafeModeActive || AegisTrustState.IsSafeMode)
            && !AegisTrustPolicy.AllowRelaxedMirrorTrust();
        SafeModeWarningPanel.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        if (!active)
        {
            return;
        }

        var reason = string.IsNullOrWhiteSpace(mirror.SafeModeReason)
            ? AegisTrustState.Reason
            : mirror.SafeModeReason;
        SafeModeWarningText.Text =
            AegisTrustState.BuildSafeModeMessage() +
            (string.IsNullOrWhiteSpace(reason) ? "" : $"\n\n원인: {reason}");
    }
}