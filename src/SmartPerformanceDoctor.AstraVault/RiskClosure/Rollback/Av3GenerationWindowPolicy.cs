namespace SmartPerformanceDoctor.AstraVault.RiskClosure.Rollback;

/// <summary>Local monotonic generation window rules (cannot detect whole-vault rewind without anchor).</summary>
public static class Av3GenerationWindowPolicy
{
    /// <summary>Target generation must be strictly greater than previous for forward commit hints.</summary>
    public static bool IsForwardJournalWindow(ulong previousGeneration, ulong targetGeneration) =>
        targetGeneration > previousGeneration;

    /// <summary>Header/metadata observed gen must not drop below last authenticated.</summary>
    public static bool IsLocalRollback(ulong lastAuthenticated, ulong observedGeneration) =>
        observedGeneration < lastAuthenticated;

    /// <summary>High generation present but not authenticated — do not trust as current open.</summary>
    public static bool IsStaleHighGeneration(ulong lastAuthenticated, ulong observedGeneration, bool authenticated) =>
        observedGeneration > lastAuthenticated && !authenticated;

    /// <summary>Equal generation requires identical root commitments/digests across copies.</summary>
    public static bool EqualGenerationRequiresMatchingRoots(
        ReadOnlySpan<byte> commitmentA,
        ReadOnlySpan<byte> commitmentB,
        ReadOnlySpan<byte> digestA,
        ReadOnlySpan<byte> digestB) =>
        commitmentA.SequenceEqual(commitmentB) && digestA.SequenceEqual(digestB);
}