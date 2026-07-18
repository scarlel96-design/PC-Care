namespace SmartPerformanceDoctor.AstraVault.RiskClosure.Rollback;

/// <summary>Rollback detection limits and S-Class anchor requirements (documentation + harness).</summary>
public static class Av3AnchorPolicy
{
    public const string FullVaultRollbackLimitation =
        "If the entire vault directory is restored to an older snapshot and no external or trusted local monotonic anchor exists, "
        + "cryptographic rollback of individual fields cannot be distinguished from a legitimate older generation. "
        + "Detection is limited to internal consistency (generation windows, digest consensus, AEAD authentication).";

    /// <summary>Whole-vault time travel without anchor is not fully detectable.</summary>
    public static bool CanDetectFullVaultRollbackWithoutAnchor => false;

    /// <summary>S-Class target requires external anchor or trusted local monotonic anchor (not implemented in E-4).</summary>
    public static bool SClassRequiresExternalOrTrustedLocalAnchor => true;

    public static bool HasTrustedAnchor(bool externalAnchorPresent, bool trustedLocalMonotonicAnchorPresent) =>
        externalAnchorPresent || trustedLocalMonotonicAnchorPresent;
}