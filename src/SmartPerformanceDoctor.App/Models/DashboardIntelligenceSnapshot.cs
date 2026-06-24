namespace SmartPerformanceDoctor.App.Models;

public sealed record DashboardIntelligenceSnapshot(
    string OverallStatus,
    string OverallSeverity,
    string Summary,
    int HealthScore,
    IReadOnlyList<string> TopIssues,
    IReadOnlyList<DashboardStatusCard> Cards,
    IReadOnlyList<DashboardAction> QuickActions,
    IReadOnlyList<DashboardAction> Actions,
    IReadOnlyList<AttentionCard> AttentionCards,
    IReadOnlyList<ReportPreview> RecentReports,
    DashboardAction? PrimaryRecommendation,
    IReadOnlyList<DashboardAction> SecondaryRecommendations,
    IReadOnlyList<string> RecentRecords);
