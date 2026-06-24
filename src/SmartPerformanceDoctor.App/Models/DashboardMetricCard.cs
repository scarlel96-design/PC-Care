namespace SmartPerformanceDoctor.App.Models;

public sealed record DashboardMetricCard(
    string Title,
    string Value,
    string Status,
    string Symbol,
    string Description);
