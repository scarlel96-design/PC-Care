namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-13 trusted anchor failure taxonomy (public codes only in reports).</summary>
public enum Av3TrustedAnchorFailureReason
{
    None = 0,
    ProviderUnavailable = 1,
    ProductionRouteDisabled = 2,
    HarnessOnlyRequired = 3,
    IsolatedRootRequired = 4,
    MachineBindingMismatch = 5,
    MachineBindingUnavailable = 6,
    ExternalWitnessUnavailable = 7,
    ExternalWitnessCounterStale = 8,
    ExternalWitnessCounterRollback = 9,
    ExternalWitnessDigestMismatch = 10,
    ExternalWitnessSignatureInvalid = 11,
    ExternalWitnessReplayDetected = 12,
    OfflineGraceWriterPromotionDenied = 13,
    RecoveryRequired = 14,
    TrustedAnchorUpdateNotCommitted = 15,
    HeaderCommitFailedRecoveryRequired = 16,
    SameDiskAnchorInsufficientForFullRollback = 17,
    ConcurrentUpdateDenied = 18,
    StateCorrupt = 19,
    CancellationAborted = 20,
    CleanupFailureAfterPrepare = 21
}