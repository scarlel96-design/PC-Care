using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmartPerformanceDoctor.App.Branding;
using SmartPerformanceDoctor.App.Services.Aegis;
using SmartPerformanceDoctor.App.Services.Commercial;

namespace SmartPerformanceDoctor.App.Views;

public sealed partial class ProgramProtectionCenterPage : Page
{
    private readonly ProgramProtectionService _service = new();

    public ProgramProtectionCenterPage()
    {
        InitializeComponent();
        AegisDisclaimerText.Text = AstraCareBranding.AegisDisclaimer;
        Verify();
    }

    private void Verify(object sender, RoutedEventArgs e) => Verify();

    private void RebuildBaseline(object sender, RoutedEventArgs e)
    {
        AegisMirrorService.Shared.RebuildBaseline(AppInfo.BuildVersion);
        Verify();
    }

    private void ExportOfflineCapsule(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = AegisMirrorService.Shared.ExportOfflineCapsule();
            MirrorStatusText.Text = "오프라인 복구 캡슐을 생성했습니다.";
            MirrorDetailText.Text = path;
        }
        catch (Exception ex)
        {
            MirrorStatusText.Text = "오프라인 캡슐 생성 실패";
            MirrorDetailText.Text = ex.Message;
        }
    }

    private void Verify()
    {
        var mirror = AegisMirrorService.Shared.RunManualCheck(AppInfo.BuildVersion, attemptRepair: true);
        MirrorStatusText.Text = mirror.Message;
        MirrorDetailText.Text =
            $"보호 등급 Level {mirror.ProtectionLevel} · 보호 파일 {mirror.ProtectedFileCount}개 · " +
            $"매니페스트 {(mirror.ManifestReady ? "준비됨" : "없음")} ({mirror.ManifestSource}) · 서명 {(mirror.ManifestSignatureValid ? "정상" : "오류")} · " +
            $"캡슐 {(mirror.CapsuleReady ? "준비됨" : "없음")} · 해시 {(mirror.CapsuleHashValid ? "정상" : "오류")} · " +
            $"키 보호 {mirror.KeyProtectionMode} · TPM {(mirror.TpmAvailable ? "가능" : "DPAPI")} · " +
            $"복구 서비스 {(mirror.RecoveryServiceInstalled ? (mirror.RecoveryServiceRunning ? "실행 중" : "중지") : "미설치")} · " +
            $"백업 슬롯 {(mirror.BackupSlotReady ? "준비" : "없음")} · 오프라인 {(mirror.OfflineCapsuleReady ? "준비" : "없음")} · " +
            $"감사체인 {(mirror.AuditChainValid ? "정상" : "오류")} · " +
            $"마지막 검사 {mirror.LastCheckAt?.ToLocalTime():yyyy-MM-dd HH:mm} · 자동 복구 {mirror.RepairedFiles}건" +
            (string.IsNullOrWhiteSpace(mirror.RecoveryReportPath) ? "" : $"\n보고서: {mirror.RecoveryReportPath}");

        var report = _service.VerifyInstallIntegrity();
        StatusText.Text = report.Message;
        ExeHashText.Text = string.IsNullOrWhiteSpace(report.ExeSha256)
            ? report.ExePath
            : $"{report.ExePath}\nSHA256: {report.ExeSha256}";

        var findings = new List<string>();
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
            : new[] { "복구 미러 · 핵심 실행 파일 · Knowledge Pack 정상" };
    }
}