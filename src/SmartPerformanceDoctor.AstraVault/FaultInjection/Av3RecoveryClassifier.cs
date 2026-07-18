namespace SmartPerformanceDoctor.AstraVault.FaultInjection;

public sealed class Av3CommitSnapshot
{
    public ulong PreviousAuthenticatedGeneration { get; set; }
    public ulong AttemptedTargetGeneration { get; set; }
    public bool ObjectsFlushed { get; set; }
    public bool MetadataFlushed { get; set; }
    public bool JournalFlushed { get; set; }
    public bool ActivationWritten { get; set; }
    public bool ActivationFlushed { get; set; }
    public bool RereadSucceeded { get; set; }
    public bool RereadFailed { get; set; }
    public bool ActivationAuthenticated { get; set; }
    public bool MetadataAuthenticated { get; set; }
    public bool EqualGenerationConflictingRoot { get; set; }
    public bool RollbackSuspected { get; set; }
    public bool RedundancyDegraded { get; set; }
    public Av3FaultPoint? FaultPoint { get; set; }
    public bool CleanupCompleted { get; set; }
    public bool CleanupFailed { get; set; }
    public bool Aborted { get; set; }
    public bool DiskFull { get; set; }
    public bool ExternalMediaRemoved { get; set; }
    public bool StaleHighGenerationUnauthenticated { get; set; }
    public bool HeaderCopyConflict { get; set; }
    public int HeaderCopyDurableCount { get; set; }
    public Av3FaultPoint? FlushFailureStep { get; set; }
}

public static class Av3RecoveryClassifier
{
    public static Av3RecoveryClassification Classify(Av3CommitSnapshot snapshot)
    {
        if (snapshot.EqualGenerationConflictingRoot || snapshot.HeaderCopyConflict)
        {
            return Av3RecoveryClassification.CorruptBlocked;
        }

        if (snapshot.RollbackSuspected)
        {
            return Av3RecoveryClassification.RollbackSuspected;
        }

        if (snapshot.StaleHighGenerationUnauthenticated)
        {
            return Av3RecoveryClassification.PreviousGenerationOpen;
        }

        if (snapshot.Aborted || snapshot.DiskFull || snapshot.ExternalMediaRemoved)
        {
            return Av3RecoveryClassification.Aborted;
        }

        if (snapshot.ActivationAuthenticated && snapshot.MetadataAuthenticated && snapshot.CleanupFailed && !snapshot.CleanupCompleted)
        {
            return Av3RecoveryClassification.RecoveryRequired;
        }

        if (snapshot.FaultPoint is not null)
        {
            return ClassifyEarlyFault(snapshot);
        }

        if (snapshot.ActivationAuthenticated && snapshot.MetadataAuthenticated && snapshot.CleanupCompleted)
        {
            if (snapshot.HeaderCopyDurableCount == 1)
            {
                return Av3RecoveryClassification.RedundancyDegraded;
            }

            return Av3RecoveryClassification.NewGenerationOpen;
        }

        if (snapshot.RereadSucceeded && (!snapshot.ActivationAuthenticated || !snapshot.MetadataAuthenticated))
        {
            return Av3RecoveryClassification.CorruptBlocked;
        }

        if (snapshot.ActivationFlushed && !snapshot.RereadSucceeded)
        {
            return Av3RecoveryClassification.RecoveryRequired;
        }

        return Av3RecoveryClassification.UnknownFailClosed;
    }

    private static Av3RecoveryClassification ClassifyEarlyFault(Av3CommitSnapshot snapshot)
    {
        if (snapshot.Aborted)
        {
            return Av3RecoveryClassification.Aborted;
        }

        return snapshot.FaultPoint switch
        {
            Av3FaultPoint.BeforeObjectWrite
                or Av3FaultPoint.BeforeMetadataWrite
                or Av3FaultPoint.BeforeJournalWrite
                or Av3FaultPoint.BeforeActivationHeaderWrite => Av3RecoveryClassification.PreviousGenerationOpen,
            Av3FaultPoint.AfterObjectWriteBeforeFlush
                or Av3FaultPoint.AfterMetadataWriteBeforeFlush
                or Av3FaultPoint.AfterJournalWriteBeforeFlush => Av3RecoveryClassification.PreviousGenerationOpen,
            Av3FaultPoint.AfterActivationHeaderWriteBeforeFlush => Av3RecoveryClassification.RecoveryRequired,
            Av3FaultPoint.AfterObjectFlush
                or Av3FaultPoint.AfterMetadataFlush
                or Av3FaultPoint.AfterJournalFlush
                or Av3FaultPoint.AfterAuthenticationBeforeCleanup => Av3RecoveryClassification.RecoveryRequired,
            Av3FaultPoint.AfterActivationFlushBeforeReread => snapshot.RereadFailed
                ? Av3RecoveryClassification.CorruptBlocked
                : Av3RecoveryClassification.RecoveryRequired,
            Av3FaultPoint.AfterRereadBeforeAuthentication => Av3RecoveryClassification.CorruptBlocked,
            Av3FaultPoint.DuringCleanup => snapshot.ActivationAuthenticated && snapshot.MetadataAuthenticated
                ? Av3RecoveryClassification.RecoveryRequired
                : Av3RecoveryClassification.RecoveryRequired,
            _ => Av3RecoveryClassification.UnknownFailClosed
        };
    }

    public static bool AllowsNormalOpen(Av3RecoveryClassification classification) =>
        classification is Av3RecoveryClassification.PreviousGenerationOpen
            or Av3RecoveryClassification.NewGenerationOpen;

    public static bool TrustsMetadata(Av3RecoveryClassification classification) =>
        classification == Av3RecoveryClassification.NewGenerationOpen;
}