namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>E-7 writer invariant identifiers (pre-enable hardening).</summary>
public enum Av3WriterInvariant
{
    VerifyBeforeCommit = 1,
    PostFlushRereadBeforeAuth = 2,
    AuthBeforeTrust = 3,
    OldGenerationPreservedUntilCommit = 4,
    NoPartialGenerationNormalOpen = 5,
    NoJournalCleartext = 6,
    NoProductionRouteWhileDisabled = 7,
    NoUiServiceRoute = 8,
    NoMigration = 9,
    NoAutoDeleteOriginal = 10,
    NoSecretLog = 11,
    CleanupFailureSeparated = 12
}