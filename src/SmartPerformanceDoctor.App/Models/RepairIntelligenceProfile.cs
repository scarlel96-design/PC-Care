namespace SmartPerformanceDoctor.App.Models;

public sealed record RepairIntelligenceProfile(
    string Area,
    string Health,
    string RootCause,
    string RecommendedPlan,
    string Confidence,
    IReadOnlyList<string> Signals);
