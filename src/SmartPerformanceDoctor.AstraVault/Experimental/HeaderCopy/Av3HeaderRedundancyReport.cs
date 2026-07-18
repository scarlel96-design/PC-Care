using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Validation;

namespace SmartPerformanceDoctor.AstraVault.Experimental.HeaderCopy;

public sealed class Av3HeaderCopyTrustEvidence
{
    public byte CopyIndex { get; init; }
    public bool StructurallyValid { get; init; }
    public bool CryptographicallyValid { get; init; }
    public ulong Generation { get; init; }
    public byte[] MetadataRootPlaintextCommitment { get; init; } = [];
    public byte[] MetadataRootCiphertextDigest { get; init; } = [];
}

/// <summary>R2/R3: 3-copy trust analysis (no repair mutations).</summary>
public static class Av3HeaderRedundancyReport
{
    public static IReadOnlyList<Av3HeaderCopyTrustEvidence> Analyze(
        VaultLocator locator,
        IReadOnlyList<(byte CopyIndex, ReadOnlyMemory<byte> CopyBytes)> copies,
        string password)
    {
        var parsed = HeaderCopySelector.ParseCandidates(locator, copies);
        var result = new List<Av3HeaderCopyTrustEvidence>();
        foreach (var candidate in parsed)
        {
            var crypto = HeaderCopySelector.TryValidateCopyCrypto(candidate.Copy, password, out var vmk);
            try
            {
                result.Add(new Av3HeaderCopyTrustEvidence
                {
                    CopyIndex = candidate.CopyIndex,
                    StructurallyValid = true,
                    CryptographicallyValid = crypto,
                    Generation = candidate.Copy.Generation,
                    MetadataRootPlaintextCommitment = candidate.Copy.MetadataRootPlaintextCommitment.ToArray(),
                    MetadataRootCiphertextDigest = candidate.Copy.MetadataRootCiphertextDigest.ToArray()
                });
            }
            finally
            {
                AstraKdf.Zero(vmk);
            }
        }

        return result;
    }

    public static IReadOnlyList<Av3HeaderConflictEvidence> FindConflicts(IReadOnlyList<Av3HeaderCopyTrustEvidence> copies)
    {
        var conflicts = new List<Av3HeaderConflictEvidence>();
        foreach (var group in copies.Where(c => c.StructurallyValid).GroupBy(c => c.Generation))
        {
            var commitments = group.Select(c => Convert.ToHexString(c.MetadataRootPlaintextCommitment)).Distinct().Count();
            var digests = group.Select(c => Convert.ToHexString(c.MetadataRootCiphertextDigest)).Distinct().Count();
            if (commitments > 1 || digests > 1)
            {
                conflicts.Add(new Av3HeaderConflictEvidence
                {
                    Generation = group.Key,
                    DistinctPlaintextCommitments = commitments,
                    DistinctCiphertextDigests = digests
                });
            }
        }

        return conflicts;
    }
}