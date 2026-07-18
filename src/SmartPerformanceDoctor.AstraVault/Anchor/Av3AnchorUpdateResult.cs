namespace SmartPerformanceDoctor.AstraVault.Anchor;

public sealed class Av3AnchorUpdateResult
{
    public bool Success { get; init; }

    public Av3AnchorFailureReason FailureReason { get; init; }

    public string PublicErrorClass { get; init; } = string.Empty;

    public Av3AnchorSnapshot? Snapshot { get; init; }
}