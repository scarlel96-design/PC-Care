namespace SmartPerformanceDoctor.AstraVault.Experimental.HeaderCopy;

/// <summary>R3: evidence of equal-generation conflicting roots (classification only).</summary>
public sealed class Av3HeaderConflictEvidence
{
    public ulong Generation { get; init; }
    public int DistinctPlaintextCommitments { get; init; }
    public int DistinctCiphertextDigests { get; init; }
    public bool HasConflict => DistinctPlaintextCommitments > 1 || DistinctCiphertextDigests > 1;
}