namespace SmartPerformanceDoctor.App.Models;

public sealed record CareStepResult(
    string Phase,
    string Title,
    string Detail,
    string ExecutionKind,
    bool Success,
    string? LogPath = null);

public sealed record CareSessionResult(
    string Scope,
    bool IncludeRepair,
    bool Completed,
    string Summary,
    int? ScoreBefore,
    int? ScoreAfter,
    IReadOnlyList<CareStepResult> Steps,
    string? SessionId = null,
    string? AuditFolder = null,
    int? HealthDelta = null,
    bool AuditChainValid = true);

public sealed record CareRequest(
    string Scope,
    bool IncludeRepair,
    bool RiskAccepted);

public sealed record CareNavigationRequest(
    string Scope,
    bool AutoStart = false,
    bool IncludeRepair = false,
    bool RiskAccepted = false,
    string? NavigationId = null)
{
    public string NavigationId { get; init; } = NavigationId ?? Guid.NewGuid().ToString("N");
}