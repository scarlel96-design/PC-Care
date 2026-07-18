namespace SmartPerformanceDoctor.AstraVault.RiskClosure.Rollback;

/// <summary>R10 rollback signals (classification only).</summary>
public sealed class Av3RollbackEvidence
{
    public ulong LastAuthenticatedGeneration { get; init; }
    public ulong ObservedHeaderGeneration { get; init; }
    public ulong ObservedMetadataGeneration { get; init; }
    public ulong ObservedJournalPreviousGeneration { get; init; }
    public ulong ObservedJournalTargetGeneration { get; init; }
    public bool HeaderMetadataGenerationMismatch { get; init; }
    public bool JournalGenerationWindowInvalid { get; init; }
    public bool EqualGenerationConflictingRoot { get; init; }
    public bool PreviousRootDigestMismatch { get; init; }
    public bool StaleHighGenerationUnauthenticated { get; init; }
    public bool StaleActivationPayload { get; init; }
    public bool OldMetadataRootReplay { get; init; }
    public bool JournalClaimsForwardCommit { get; init; }
    public bool ActivationAuthenticated { get; init; }
    public bool FullVaultRollbackWithoutAnchorSuspected { get; init; }
}