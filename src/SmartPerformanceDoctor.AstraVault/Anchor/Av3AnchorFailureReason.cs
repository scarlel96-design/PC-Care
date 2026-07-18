namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-11 anchor operation failure taxonomy (public classification only).</summary>
public enum Av3AnchorFailureReason
{
    None = 0,
    ProductionRouteDisabled = 1,
    HarnessOnlyRequired = 2,
    IsolatedRootRequired = 3,
    AnchorUnavailable = 4,
    UpdateInFlight = 5,
    ReentrantUpdate = 6,
    DuplicateUpdateId = 7,
    PendingUpdateMissing = 8,
    StateCorrupt = 9,
    MonotonicityViolation = 10,
    GenerationMismatch = 11,
    WitnessDigestMismatch = 12,
    StaleWitness = 13,
    UpdateBeforeCommit = 14
}