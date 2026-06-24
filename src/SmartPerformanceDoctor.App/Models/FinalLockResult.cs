namespace SmartPerformanceDoctor.App.Models;

public sealed record FinalLockResult(
    string Version,
    string Status,
    string Confidence,
    string Summary,
    IReadOnlyList<FinalLockGateItem> Gates,
    IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<string> RemainingManualChecks);
