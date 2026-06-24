namespace SmartPerformanceDoctor.App.Models.Update;

public sealed record UpdateApplyResult(
    bool Success,
    string FromVersion,
    string ToVersion,
    string Message,
    bool RestartScheduled,
    int FilesApplied,
    int FilesDeferred);