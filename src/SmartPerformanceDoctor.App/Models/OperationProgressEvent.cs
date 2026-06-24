namespace SmartPerformanceDoctor.App.Models;

public sealed record OperationProgressEvent(
    DateTimeOffset Timestamp,
    string OperationId,
    string Source,
    string Phase,
    string Status,
    double Progress,
    string Message,
    bool CanCancel);
