using SmartPerformanceDoctor.Contracts;

namespace SmartPerformanceDoctor.App.Models;

public sealed record InferenceInsight(
    string Category,
    string Title,
    string Detail,
    double Confidence,
    string Source);

public sealed record InferenceResult(
    string Scope,
    int FusedScore,
    string Status,
    string Summary,
    IReadOnlyList<InferenceInsight> Insights,
    IReadOnlyList<string> RecommendedRepairActionIds,
    IntelligenceSummary? EnhancedIntelligence);