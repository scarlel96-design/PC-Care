namespace SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;

/// <summary>
/// Phase E-4 decision: v1 journal uses fixed <see cref="Journal.Av3JournalDescriptor"/> (digests, ids, state only).
/// Optional AEAD journal body is a future extension — not required for R11 closure in E-4.
/// </summary>
public static class Av3JournalDigestOnlyPolicy
{
    public const bool V1DigestOnlyDescriptor = true;
    public const bool V1OptionalAeadBodyAllowed = false;
    public const bool CleartextPathsFilenamesForbidden = true;
    public const bool CleartextPasswordOrKeysForbidden = true;

    public static string PolicySummary =>
        "Journal stores container/transaction UUIDs, generation counters, SHA-256 digests, and state enum only. "
        + "No filenames, folder names, user paths, extensions, or plaintext metadata. "
        + "Optional AEAD envelope for journal body is deferred; descriptor remains digest-only.";
}