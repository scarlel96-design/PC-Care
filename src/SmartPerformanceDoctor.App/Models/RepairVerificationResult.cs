namespace SmartPerformanceDoctor.App.Models;

public sealed record RepairVerificationResult(
    string OperationId,
    string Area,
    string Action,
    string Status,
    string Confidence,
    string Summary,
    string BeforeSnapshot,
    string AfterSnapshot,
    string LogPath,
    IReadOnlyList<RepairEvidence> Evidence,
    IReadOnlyList<string> NextActions);
