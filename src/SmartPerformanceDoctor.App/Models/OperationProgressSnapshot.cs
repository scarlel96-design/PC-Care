namespace SmartPerformanceDoctor.App.Models;

public sealed record OperationProgressSnapshot(
    string ActiveOperationId,
    string OverallStatus,
    double OverallProgress,
    bool HasActiveOperation,
    bool CanCancel,
    IReadOnlyList<OperationProgressEvent> Events);
