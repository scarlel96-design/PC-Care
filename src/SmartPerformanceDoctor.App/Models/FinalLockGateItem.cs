namespace SmartPerformanceDoctor.App.Models;

public sealed record FinalLockGateItem(
    string Name,
    string Category,
    string Status,
    string Severity,
    string Message,
    string Evidence);
