using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Repair;

namespace SmartPerformanceDoctor.AstraVault.Experimental.HeaderCopy;

/// <summary>R2/R3 header redundancy and conflict closure (classification only).</summary>
public static class Av3HeaderRepairClassifier
{
    /// <summary>Lowest copy index wins among cryptographically valid matching copies (first-durable authoritative policy).</summary>
    public static Av3HeaderRepairPlan Plan(
        IReadOnlyList<Av3HeaderCopyTrustEvidence> copies,
        ulong lastAuthenticatedGeneration,
        bool activationAuthenticated)
    {
        var conflicts = Av3HeaderRedundancyReport.FindConflicts(copies);
        if (conflicts.Count > 0)
        {
            return Blocked(Av3RecoveryClassification.CorruptBlocked, Av3RepairClassification.CorruptBlocked, "equal_generation_conflicting_root");
        }

        var valid = copies.Where(c => c.CryptographicallyValid).OrderBy(c => c.CopyIndex).ToList();
        if (valid.Count == 0)
        {
            return Blocked(Av3RecoveryClassification.CorruptBlocked, Av3RepairClassification.CorruptBlocked, "zero_valid_copy");
        }

        var currentGen = valid.Max(c => c.Generation);
        if (currentGen > lastAuthenticatedGeneration && !activationAuthenticated)
        {
            return Blocked(
                Av3RecoveryClassification.PreviousGenerationOpen,
                Av3RepairClassification.ManualReviewRequired,
                "unauthenticated_high_generation");
        }

        var currentValid = valid.Where(c => c.Generation == currentGen).ToList();
        var matchingGroups = currentValid
            .GroupBy(c => (
                Commit: Convert.ToHexString(c.MetadataRootPlaintextCommitment),
                Digest: Convert.ToHexString(c.MetadataRootCiphertextDigest)))
            .OrderByDescending(g => g.Count())
            .First();

        var matching = matchingGroups.ToList();
        var stalePresent = copies.Any(c => c.StructurallyValid && c.Generation < currentGen);

        if (matching.Count >= 2 && stalePresent)
        {
            return PlanWith(
                matching,
                Av3RecoveryClassification.NewGenerationOpen,
                Av3RepairClassification.RepairRecommended,
                ["refresh_stale_header_copies"],
                stalePresent: true);
        }

        return matching.Count switch
        {
            >= 3 => PlanWith(matching, Av3RecoveryClassification.NewGenerationOpen, Av3RepairClassification.Healthy, ["none"]),
            2 => PlanWith(matching, Av3RecoveryClassification.NewGenerationOpen, Av3RepairClassification.Healthy, ["none"]),
            1 => PlanWith(matching, Av3RecoveryClassification.RedundancyDegraded, Av3RepairClassification.RedundancyDegraded, ["backfill_header_copies"]),
            _ => Blocked(Av3RecoveryClassification.CorruptBlocked, Av3RepairClassification.CorruptBlocked, "no_matching_current_copy")
        };
    }

    public static Av3HeaderRepairPlan PlanRollbackSuspected(ulong headerGeneration, ulong lastAuthenticatedGeneration)
    {
        if (headerGeneration < lastAuthenticatedGeneration)
        {
            return new Av3HeaderRepairPlan
            {
                RecoveryOutcome = Av3RecoveryClassification.RollbackSuspected,
                RepairPosture = Av3RepairClassification.RollbackSuspected,
                RecommendedActions = ["manual_review_generation_chain"]
            };
        }

        return new Av3HeaderRepairPlan
        {
            RecoveryOutcome = Av3RecoveryClassification.UnknownFailClosed,
            RepairPosture = Av3RepairClassification.ManualReviewRequired,
            RecommendedActions = ["manual_review_stale_high_generation"]
        };
    }

    private static Av3HeaderRepairPlan PlanWith(
        IReadOnlyList<Av3HeaderCopyTrustEvidence> matching,
        Av3RecoveryClassification recovery,
        Av3RepairClassification repair,
        IReadOnlyList<string> actions,
        bool stalePresent = false) =>
        new()
        {
            RecoveryOutcome = recovery,
            RepairPosture = repair,
            AuthoritativeCopyIndex = matching.Min(c => c.CopyIndex),
            ValidMatchingCopyCount = matching.Count,
            StaleCopiesPresent = stalePresent,
            RecommendedActions = actions
        };

    private static Av3HeaderRepairPlan Blocked(
        Av3RecoveryClassification recovery,
        Av3RepairClassification repair,
        string action) =>
        new()
        {
            RecoveryOutcome = recovery,
            RepairPosture = repair,
            RecommendedActions = [action]
        };
}