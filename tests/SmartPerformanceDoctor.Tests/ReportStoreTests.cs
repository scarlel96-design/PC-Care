using SmartPerformanceDoctor.App.Services;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class ReportStoreTests
{
    [Fact]
    public void LoadRecentReports_IncludesSystemCareAndUnifiedCareAuditReports()
    {
        var root = Path.Combine(Path.GetTempPath(), "pccare-report-store-" + Guid.NewGuid().ToString("N"));
        var legacyReports = Path.Combine(root, "legacy");
        var careReports = Path.Combine(root, "care");
        var systemFolder = Path.Combine(careReports, "system_care", "smart", "20260726_120000");
        var sessionFolder = Path.Combine(careReports, "unified_care", "sessions", "care-test");

        try
        {
            Directory.CreateDirectory(systemFolder);
            Directory.CreateDirectory(sessionFolder);
            File.WriteAllText(Path.Combine(systemFolder, "scan_report.json"),
                """{"moduleTitle":"스마트 검사","summary":"정리 후보를 찾았습니다.","healthScore":81,"healthGrade":"양호"}""");
            File.WriteAllText(Path.Combine(sessionFolder, "session_report.json"),
                """{"scope":"full","completed":true,"summary":"점검·복구 완료","scoreBefore":68,"scoreAfter":92,"stepCount":5,"completedAt":"2026-07-26T12:01:00+09:00"}""");

            var reports = new ReportStore(legacyReports, careReports).LoadRecentReports();

            Assert.Equal(2, reports.Count);
            Assert.Contains(reports, report => report.Module == "스마트 검사" && report.HealthScore == 81);
            Assert.Contains(reports, report => report.Module == "전체 점검" && report.HealthScore == 92 && report.StatusLabel == "완료");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}