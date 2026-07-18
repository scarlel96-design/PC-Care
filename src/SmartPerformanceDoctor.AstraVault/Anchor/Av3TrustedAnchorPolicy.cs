using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-13 trusted anchor production target policy (hybrid design; route disabled).</summary>
public static class Av3TrustedAnchorPolicy
{
    public const Av3TrustedAnchorProviderKind ProductionDesignTarget =
        Av3TrustedAnchorProviderKind.HybridPolicyCoordinator;

    public static bool SameDiskLocalCannotCloseFullVaultRollback =>
        Av3PhaseGate.E13TrustedAnchorProviderPackageComplete;

    public static bool FullVaultRollbackRequiresExternalOrHybridWitness =>
        !Av3PhaseGate.E131TrustedAnchorSignoffGateComplete || Av3PhaseGate.E13TrustedAnchorProviderPackageComplete;

    public static bool ExternalWitnessUnavailableBlocksProductionEnable => true;

    public static bool TrustedAnchorUpdateFailureBlocksPromotion => true;

    public const bool ProductionAnchorImplemented = false;

    public static bool TrustedMonotonicProductionAnchorImplementationCandidate =>
        Av3PhaseGate.TrustedMonotonicProductionAnchorImplementationCandidate;

    public static bool StoresSecrets => false;

    public static bool StoresPathsOrFilenames => false;
}