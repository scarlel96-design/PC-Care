using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Journal;

/// <summary>Structural journal validation — not root of trust (activation header is).</summary>
public static class Av3JournalValidator
{
    public static void ValidateForRecovery(
        Av3JournalDescriptor journal,
        Guid expectedContainerId,
        ulong lastAuthenticatedGeneration)
    {
        if (journal.ContainerId != expectedContainerId)
        {
            throw new CryptographicException("Journal container mismatch.");
        }

        if (journal.TargetGeneration < journal.PreviousGeneration)
        {
            throw new CryptographicException("Journal generation rollback.");
        }

        if (journal.State is Av3JournalState.Aborted or Av3JournalState.Stale)
        {
            throw new CryptographicException("Journal stale or aborted.");
        }

        if (journal.TargetGeneration <= lastAuthenticatedGeneration
            && journal.State == Av3JournalState.Committed)
        {
            throw new CryptographicException("Journal replay stale.");
        }

        if (journal.ActivationTarget != Av3JournalDescriptor.ActivationTargetMetadataRoot)
        {
            throw new CryptographicException("Journal activation target invalid.");
        }
    }
}