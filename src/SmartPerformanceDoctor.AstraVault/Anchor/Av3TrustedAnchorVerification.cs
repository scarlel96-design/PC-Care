using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Trusted anchor verification outcome (maps to public anchor posture).</summary>
public sealed class Av3TrustedAnchorVerification
{
    public bool Verified { get; init; }

    public Av3AnchorStatus AnchorStatus { get; init; }

    public Av3TrustedAnchorFailureReason FailureReason { get; init; }

    public Av3TrustedAnchorWitness? Witness { get; init; }

    public bool FullVaultRollbackSuspected { get; init; }

    public bool ProductionEnableAllowed { get; init; }

    public bool WriterTrustedPromotionAllowed { get; init; }

    public string PublicSummary { get; init; } = string.Empty;
}