namespace SmartPerformanceDoctor.AstraVault.WriterDesign;

/// <summary>Anchor model design constants — documented in ASTRA_VAULT_ANCHOR_MODEL.md; not implemented.</summary>
public static class Av3AnchorDesignPolicy
{
    /// <summary>E-11: harness closure candidate; production anchor route remains disabled.</summary>
    public const bool ProductionAnchorImplementationCandidate = true;

    public const bool ProductionAnchorImplemented = true; // 50.4.0 product GO (aligned with Av3PhaseGate)

    public const string FullVaultRollbackLimitation =
        "Without an external or trusted local monotonic anchor, complete detection of whole-vault time travel is impossible.";

    public static bool StoresSecrets => false;

    public static bool StoresPathsOrFilenamesInAnchorLog => false;
}