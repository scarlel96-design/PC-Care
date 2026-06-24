namespace SmartPerformanceDoctor.App.Models;

public sealed record AttentionCard(
    string Id,
    string Title,
    string Description,
    string Severity,
    string TargetPage);