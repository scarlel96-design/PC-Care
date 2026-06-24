using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed class CoreDashboardBridgeService
{
    public CoreDiagnosticSnapshot BuildSnapshot()
    {
        var enginePath = RuntimePaths.ResolveEnginePath("smart_performance_doctor_core.exe");
        var engineExists = File.Exists(enginePath);
        var latestReport = FindLatestReport();

        var metrics = new List<CoreDiagnosticMetric>
        {
            BuildEngineMetric(enginePath, engineExists),
            BuildLatestReportMetric(latestReport),
            BuildMemoryMetric(),
            BuildSystemDriveMetric()
        };

        var failCount = metrics.Count(x => string.Equals(x.Status, "FAIL", StringComparison.OrdinalIgnoreCase));
        var warnCount = metrics.Count(x => string.Equals(x.Status, "WARN", StringComparison.OrdinalIgnoreCase));

        var status = failCount > 0 ? "주의 필요" : warnCount > 0 ? "점검 권장" : "정상";
        var health = failCount > 0 ? "critical" : warnCount > 0 ? "medium" : "low";
        var summary = engineExists
            ? (string.IsNullOrWhiteSpace(latestReport)
                ? "진단 엔진 준비됨 · 최근 보고서 없음"
                : $"진단 엔진 준비됨 · 최근 보고서 {Path.GetFileName(Path.GetDirectoryName(latestReport))}")
            : "진단 엔진 파일이 없습니다 · 다시 설치가 필요합니다";

        return new CoreDiagnosticSnapshot(status, health, summary, enginePath, latestReport, metrics);
    }

    private static CoreDiagnosticMetric BuildEngineMetric(string enginePath, bool engineExists) =>
        new("진단 엔진", engineExists ? "준비됨" : "없음", engineExists ? "OK" : "FAIL", engineExists ? "low" : "critical", engineExists ? "정상" : "엔진 파일 누락");

    private static CoreDiagnosticMetric BuildLatestReportMetric(string latestReport) =>
        new("최근 보고서", string.IsNullOrWhiteSpace(latestReport) ? "없음" : "있음", string.IsNullOrWhiteSpace(latestReport) ? "WARN" : "OK", string.IsNullOrWhiteSpace(latestReport) ? "medium" : "low", string.IsNullOrWhiteSpace(latestReport) ? "점검 후 생성됩니다" : Path.GetFileName(latestReport));

    private static CoreDiagnosticMetric BuildMemoryMetric()
    {
        var workingSetMb = Environment.WorkingSet / 1024 / 1024;
        return new CoreDiagnosticMetric("메모리 사용", $"{workingSetMb}MB", workingSetMb > 1200 ? "WARN" : "OK", workingSetMb > 1200 ? "medium" : "low", "앱 메모리");
    }

    private static CoreDiagnosticMetric BuildSystemDriveMetric()
    {
        try
        {
            var systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            var drive = new DriveInfo(systemRoot);
            var freeGb = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
            var totalGb = drive.TotalSize / 1024d / 1024d / 1024d;
            var freePercent = totalGb <= 0 ? 0 : freeGb / totalGb * 100d;
            return new CoreDiagnosticMetric("시스템 드라이브", $"{freeGb:0}GB 여유", freePercent < 10 ? "WARN" : "OK", freePercent < 10 ? "high" : "low", $"전체 {totalGb:0}GB");
        }
        catch (Exception ex)
        {
            return new CoreDiagnosticMetric("시스템 드라이브", "확인 불가", "WARN", "medium", ex.Message);
        }
    }

    private static string FindLatestReport()
    {
        var candidates = new List<string>();
        var desktopReports = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor", "Reports");
        if (Directory.Exists(desktopReports))
        {
            candidates.AddRange(Directory.GetFiles(desktopReports, "report.html", SearchOption.AllDirectories));
        }

        return candidates.OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() ?? "";
    }
}