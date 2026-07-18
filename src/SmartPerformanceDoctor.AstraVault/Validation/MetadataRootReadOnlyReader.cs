using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Format;

namespace SmartPerformanceDoctor.AstraVault.Validation;

/// <summary>Read-only metadata-root ciphertext authenticate/decrypt/validate path (Phase D).</summary>
public static class MetadataRootReadOnlyReader
{
    public const ulong DefaultLogicalId = 1;

    public static MetadataRootValidationResult Validate(
        VaultHeaderCopy header,
        ReadOnlySpan<byte> vmk,
        ReadOnlySpan<byte> metadataRootBytes,
        ulong metadataRootLogicalId = DefaultLogicalId)
    {
        MetadataRootCiphertextEnvelope envelope;
        try
        {
            envelope = MetadataRootCiphertextEnvelope.Parse(metadataRootBytes);
        }
        catch (CryptographicException ex)
        {
            throw new UnlockValidationException(ex);
        }

        var descriptor = envelope.Descriptor;
        if (descriptor.ContainerId != header.ContainerId)
        {
            throw new UnlockValidationException();
        }

        var digest = SHA256.HashData(envelope.Ciphertext);
        if (!digest.AsSpan().SequenceEqual(descriptor.MetadataCiphertextDigest)
            || !digest.AsSpan().SequenceEqual(header.MetadataRootCiphertextDigest))
        {
            throw new UnlockValidationException();
        }

        var plaintext = MetadataRootAead.AuthenticateAndDecrypt(
            header,
            descriptor,
            vmk,
            envelope.Ciphertext,
            metadataRootLogicalId);

        var fields = MetadataRootPlaintext.ReadFields(plaintext);
        GenerationRollbackRules.ValidateHeaderMetadataChain(header, descriptor);

        return new MetadataRootValidationResult
        {
            Authenticated = true,
            Descriptor = descriptor,
            RootFields = fields,
            CiphertextDigest = digest,
            RootPlaintextCommitment = fields.RootPlaintextCommitment
        };
    }
}