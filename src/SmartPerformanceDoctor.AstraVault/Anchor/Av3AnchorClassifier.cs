using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Maps anchor verification/failure evidence to <see cref="Av3AnchorStatus"/>.</summary>
public static class Av3AnchorClassifier
{
    public static Av3AnchorStatus ClassifyVerification(Av3AnchorVerificationResult result) => result.Status;

    public static Av3AnchorStatus ClassifyFailure(Av3AnchorFailureReason reason) =>
        reason switch
        {
            Av3AnchorFailureReason.None => Av3AnchorStatus.AnchorFresh,
            Av3AnchorFailureReason.ProductionRouteDisabled
                or Av3AnchorFailureReason.HarnessOnlyRequired
                or Av3AnchorFailureReason.IsolatedRootRequired => Av3AnchorStatus.AnchorUnsupported,
            Av3AnchorFailureReason.AnchorUnavailable => Av3AnchorStatus.AnchorUnavailable,
            Av3AnchorFailureReason.StateCorrupt => Av3AnchorStatus.AnchorRecoveryRequired,
            Av3AnchorFailureReason.MonotonicityViolation => Av3AnchorStatus.AnchorRollbackSuspected,
            Av3AnchorFailureReason.GenerationMismatch
                or Av3AnchorFailureReason.WitnessDigestMismatch => Av3AnchorStatus.AnchorMismatch,
            Av3AnchorFailureReason.StaleWitness => Av3AnchorStatus.AnchorStale,
            Av3AnchorFailureReason.UpdateBeforeCommit
                or Av3AnchorFailureReason.UpdateInFlight
                or Av3AnchorFailureReason.ReentrantUpdate
                or Av3AnchorFailureReason.DuplicateUpdateId
                or Av3AnchorFailureReason.PendingUpdateMissing => Av3AnchorStatus.AnchorRecoveryRequired,
            _ => Av3AnchorStatus.AnchorUnavailable
        };

    public static Av3AnchorVerificationResult BuildVerification(
        Av3AnchorSnapshot? snapshot,
        ulong observedGeneration,
        ReadOnlySpan<byte> witnessDigest)
    {
        if (snapshot is null)
        {
            return new Av3AnchorVerificationResult
            {
                Verified = false,
                Status = Av3AnchorStatus.AnchorUnavailable,
                FailureReason = Av3AnchorFailureReason.AnchorUnavailable,
                PublicSummary = "anchor_unavailable"
            };
        }

        if (!DigestMatches(snapshot.WitnessDigestHex, witnessDigest))
        {
            return new Av3AnchorVerificationResult
            {
                Verified = false,
                Status = Av3AnchorStatus.AnchorMismatch,
                FailureReason = Av3AnchorFailureReason.WitnessDigestMismatch,
                Snapshot = snapshot,
                PublicSummary = "anchor_witness_mismatch"
            };
        }

        if (observedGeneration < snapshot.Generation)
        {
            return new Av3AnchorVerificationResult
            {
                Verified = false,
                Status = Av3AnchorStatus.AnchorRollbackSuspected,
                FailureReason = Av3AnchorFailureReason.MonotonicityViolation,
                Snapshot = snapshot,
                PublicSummary = "anchor_monotonicity_violation"
            };
        }

        if (observedGeneration > snapshot.Generation)
        {
            return new Av3AnchorVerificationResult
            {
                Verified = false,
                Status = Av3AnchorStatus.AnchorStale,
                FailureReason = Av3AnchorFailureReason.StaleWitness,
                Snapshot = snapshot,
                PublicSummary = "anchor_stale"
            };
        }

        return new Av3AnchorVerificationResult
        {
            Verified = true,
            Status = Av3AnchorStatus.AnchorFresh,
            FailureReason = Av3AnchorFailureReason.None,
            Snapshot = snapshot,
            PublicSummary = "anchor_fresh"
        };
    }

    private static bool DigestMatches(string digestHex, ReadOnlySpan<byte> witnessDigest) =>
        string.Equals(digestHex, Convert.ToHexString(witnessDigest), StringComparison.OrdinalIgnoreCase);
}