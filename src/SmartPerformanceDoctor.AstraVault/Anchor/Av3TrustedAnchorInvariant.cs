namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-13 trusted anchor invariant identifiers.</summary>
public enum Av3TrustedAnchorInvariant
{
    TrustedAnchorRequiredForFullRollbackClosure = 1,
    SameDiskAnchorCannotCloseFullRollback = 2,
    ExternalWitnessCounterMonotonic = 3,
    ExternalWitnessDigestAuthenticated = 4,
    ExternalWitnessReplayRejected = 5,
    TrustedAnchorUpdateFailureNotCommitted = 6,
    TrustedAnchorUnavailableNoProductionEnable = 7,
    TrustedAnchorOfflineModeNoWriterPromotion = 8,
    FullVaultRollbackDetectedRequiresRecovery = 9,
    TrustedAnchorPublicErrorSafe = 10,
    TrustedAnchorNoSecretLeak = 11,
    ProductionAnchorNotMarkedImplementedWithoutSignoff = 12
}