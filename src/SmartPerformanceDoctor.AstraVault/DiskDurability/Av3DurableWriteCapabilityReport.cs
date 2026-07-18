namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>Aggregated durable-write capability report (harness/review only).</summary>
public sealed class Av3DurableWriteCapabilityReport
{
    public bool Passed { get; init; }

    public bool FlushRereadVerified { get; init; }

    public bool RenameReplaceVerified { get; init; }

    public bool DirectorySyncClassified { get; init; }

    public bool TrustedPromotionAllowed { get; init; }

    public Av3DiskDurabilityFailureReason FailureReason { get; init; }

    public string PublicSummary { get; init; } = string.Empty;
}