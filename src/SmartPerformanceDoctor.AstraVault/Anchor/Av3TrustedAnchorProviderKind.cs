namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-13 trusted anchor provider taxonomy (production route disabled until sign-off).</summary>
public enum Av3TrustedAnchorProviderKind
{
    NullUnavailable = 0,
    SameDiskLocalUntrusted = 1,
    HarnessSynthetic = 2,
    MachineLocalCandidate = 3,
    ExternalWitnessCandidate = 4,
    HybridPolicyCoordinator = 5
}