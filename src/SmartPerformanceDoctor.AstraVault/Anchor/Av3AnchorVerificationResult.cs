using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

public sealed class Av3AnchorVerificationResult
{
    public bool Verified { get; init; }

    public Av3AnchorStatus Status { get; init; }

    public Av3AnchorFailureReason FailureReason { get; init; }

    public Av3AnchorSnapshot? Snapshot { get; init; }

    public string PublicSummary { get; init; } = string.Empty;
}