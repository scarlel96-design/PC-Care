namespace SmartPerformanceDoctor.App.Models;

public sealed record DashboardAction(
    string Id,
    string Title,
    string Description,
    string Symbol,
    string TargetPage,
    string Risk,
    bool AutoStart = false,
    bool IncludeRepair = false,
    bool RiskAccepted = false);
