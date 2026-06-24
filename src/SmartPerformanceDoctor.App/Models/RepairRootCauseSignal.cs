namespace SmartPerformanceDoctor.App.Models;

public sealed record RepairRootCauseSignal(
    string Area,
    string Signal,
    int Weight,
    string Severity,
    string Evidence);
