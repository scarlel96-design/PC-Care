using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Format;

namespace SmartPerformanceDoctor.AstraVault.Validation;

public static class GenerationRollbackRules
{
    /// <summary>
    /// Rejects rollback / mixed-generation states. Highest header generation alone is not trusted.
    /// </summary>
    public static void ValidateHeaderMetadataChain(
        VaultHeaderCopy selected,
        MetadataRootDescriptor? metadataRoot)
    {
        if (selected.MetadataGeneration < selected.ParentMetadataGeneration)
        {
            throw new UnlockValidationException();
        }

        if (metadataRoot is null)
        {
            return;
        }

        if (metadataRoot.Generation != selected.MetadataGeneration)
        {
            throw new UnlockValidationException();
        }

        if (metadataRoot.ParentGeneration != selected.ParentMetadataGeneration)
        {
            throw new UnlockValidationException();
        }

        if (!metadataRoot.MetadataCiphertextDigest.AsSpan().SequenceEqual(selected.MetadataRootCiphertextDigest))
        {
            throw new UnlockValidationException();
        }

        if (metadataRoot.Generation < metadataRoot.ParentGeneration)
        {
            throw new UnlockValidationException();
        }
    }

    public static void ValidateEqualGenerationConflictingRoots(IReadOnlyList<VaultHeaderCopy> copies)
    {
        foreach (var group in copies.GroupBy(c => c.Generation))
        {
            var roots = group.Select(c => Convert.ToHexString(c.MetadataRootPlaintextCommitment)).Distinct().ToList();
            var digests = group.Select(c => Convert.ToHexString(c.MetadataRootCiphertextDigest)).Distinct().ToList();
            if (roots.Count > 1 || digests.Count > 1)
            {
                throw new UnlockValidationException();
            }
        }
    }

    public static void ValidateCopyConsensus(IReadOnlyList<VaultHeaderCopy> structurallyValid)
    {
        if (structurallyValid.Count == 0)
        {
            throw new UnlockValidationException();
        }

        var commitment = structurallyValid[0].MetadataRootPlaintextCommitment;
        var digest = structurallyValid[0].MetadataRootCiphertextDigest;
        foreach (var copy in structurallyValid)
        {
            if (!copy.MetadataRootPlaintextCommitment.AsSpan().SequenceEqual(commitment)
                || !copy.MetadataRootCiphertextDigest.AsSpan().SequenceEqual(digest))
            {
                throw new UnlockValidationException();
            }
        }
    }
}