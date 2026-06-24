using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Resources;

namespace SmartPerformanceDoctor.App.Services;

public sealed class DashboardIntelligenceService
{
    private readonly ReportStore _reportStore = new();
    private readonly RepairLogStore _repairLogStore = new();
    private readonly CrashLogStore _crashLogStore = new();
    private readonly CoreDashboardBridgeService _coreBridgeService = new();
    private readonly KnowledgeService _knowledge = KnowledgeService.Shared;

    public DashboardIntelligenceSnapshot BuildSnapshot()
    {
        _knowledge.EnsureRulesLoaded();

        var reports = _reportStore.LoadRecentReports();
        var repairLogs = _repairLogStore.LoadRecentLogs();
        var crashLogs = _crashLogStore.LoadRecentCrashLogs();
        var coreSnapshot = _coreBridgeService.BuildSnapshot();
        var overallStatus = crashLogs.Count > 0
            ? "오류 기록 확인 필요"
            : repairLogs.Count == 0 && reports.Count == 0
                ? "처음 점검을 시작해 보세요"
                : coreSnapshot.Health is "high" or "critical"
                    ? "주의가 필요합니다"
                    : "양호";

        var overallSeverity = crashLogs.Count > 0
            ? "high"
            : coreSnapshot.Health is "high" or "critical"
                ? "medium"
                : "low";

        var healthScore = MapHealthScore(overallSeverity);
        var topIssues = BuildTopIssues(crashLogs.Count, coreSnapshot.Health, reports.Count);
        var quickActions = BuildQuickActions();
        var attentionCards = BuildAttentionCards(crashLogs.Count, coreSnapshot.Health);
        var recentReports = reports.Take(3)
            .Select(r => new ReportPreview(r.Title, r.CreatedAt, "ReportPage"))
            .ToList();
        var primary = new DashboardAction(
            "deep-scan",
            "정밀 점검·복구 시작",
            "전체 점검 후 문제가 있으면 안전 복구까지 자동 진행합니다. PC 설정이 일부 변경될 수 있습니다.",
            "◎",
            "UnifiedCarePage:full",
            "medium",
            AutoStart: true,
            IncludeRepair: true,
            RiskAccepted: true);
        var secondary = new List<DashboardAction>
        {
            new("care", "시스템 케어", "정리·최적화가 필요할 때", "◉", "SystemCareCenterPage", "low"),
            new("reports", "보고서 보기", "최근 점검 결과 확인", "□", "ReportPage", "low")
        };

        return new DashboardIntelligenceSnapshot(
            overallStatus,
            overallSeverity,
            BuildSummary(overallStatus, reports.Count, repairLogs.Count, crashLogs.Count),
            healthScore,
            topIssues,
            Array.Empty<DashboardStatusCard>(),
            quickActions,
            secondary,
            attentionCards,
            recentReports,
            primary,
            secondary,
            Array.Empty<string>());
    }

    private static int MapHealthScore(string severity) => severity switch
    {
        "high" => 42,
        "medium" => 68,
        _ => 88
    };

    private static IReadOnlyList<string> BuildTopIssues(int crashCount, string coreHealth, int reportCount)
    {
        var issues = new List<string>();
        if (crashCount > 0)
        {
            issues.Add("오류 기록 확인이 필요합니다");
        }

        if (coreHealth is "high" or "critical")
        {
            issues.Add("시스템 점검에서 주의 항목이 발견되었습니다");
        }

        if (reportCount == 0)
        {
            issues.Add("아직 점검 보고서가 없습니다 — 정밀 점검을 권장합니다");
        }

        if (issues.Count == 0)
        {
            issues.Add("현재 특별한 문제 징후가 없습니다");
        }

        return issues.Take(3).ToArray();
    }

    private static IReadOnlyList<DashboardAction> BuildQuickActions() =>
    [
        new("slow", "내 PC가 느려요", "빠른 점검으로 병목을 찾습니다", "◌", "UnifiedCarePage:quick", "low"),
        new("audio", "소리가 안 나와요", "오디오 스택을 점검합니다", "◉", "UnifiedCarePage:audio", "medium"),
        new("driver", "드라이버가 이상해요", "드라이버·PnP 상태를 확인합니다", "◇", "UnifiedCarePage:driver", "medium"),
        new("storage", "저장 공간을 정리하고 싶어요", "시스템 케어로 정리합니다", "▣", "SystemCareCenterPage", "low"),
        new("vault", "민감 파일을 보호하고 싶어요", "보안 금고로 암호화", "⬢", "SecureVaultCenterPage", "high"),
        new("delete", "파일을 완전히 삭제하고 싶어요", "보안 삭제", "⊗", "SecureDeleteCenterPage", "high")
    ];

    private static IReadOnlyList<AttentionCard> BuildAttentionCards(int crashCount, string coreHealth)
    {
        var cards = new List<AttentionCard>();
        if (crashCount > 0)
        {
            cards.Add(new("crash", "오류 기록 확인 필요", "최근 오류가 기록되어 있습니다.", "high", "CrashLogPage"));
        }

        if (coreHealth is "high" or "critical")
        {
            cards.Add(new("health", "시스템 주의", "점검 결과 주의 항목이 있습니다.", "medium", "UnifiedCarePage:system"));
        }

        return cards;
    }

    private static string BuildSummary(
        string status,
        int reportCount,
        int repairLogCount,
        int crashLogCount)
    {
        return $"{status} · 보고서 {reportCount}건 · 복구 {repairLogCount}건 · 오류 {crashLogCount}건";
    }
}