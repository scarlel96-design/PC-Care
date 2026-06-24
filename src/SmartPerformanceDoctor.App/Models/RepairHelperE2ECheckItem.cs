namespace SmartPerformanceDoctor.App.Models;

public sealed record RepairHelperE2ECheckItem(
    string Name,
    string Area,
    string Action,
    string Status,
    string Severity,
    string Message,
    string EvidencePath);
