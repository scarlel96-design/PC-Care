namespace SmartPerformanceDoctor.App.Models;

public sealed record DashboardStatusCard(
    string Id,
    string Title,
    string Value,
    string Status,
    string Severity,
    string Symbol,
    string Description,
    string Detail);
