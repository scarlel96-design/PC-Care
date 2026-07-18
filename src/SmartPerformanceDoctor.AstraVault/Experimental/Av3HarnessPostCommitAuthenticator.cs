using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Validation;

namespace SmartPerformanceDoctor.AstraVault.Experimental;

/// <summary>Full read-only trust chain after post-flush reread (test harness only).</summary>
public static class Av3HarnessPostCommitAuthenticator
{
    public static Av3HarnessAuthenticationResult AuthenticateFullChain(
        ReadOnlySpan<byte> headerCopyBytes,
        ReadOnlySpan<byte> metadataRootEnvelope,
        ReadOnlySpan<byte> vmk)
    {
        var result = new Av3HarnessAuthenticationResult();
        try
        {
            var header = VaultHeaderCopy.Parse(headerCopyBytes, (uint)headerCopyBytes.Length);
            var activationPlain = HeaderActivationAead.AuthenticateAndDecrypt(header, vmk);
            result = result with { ActivationAeadAuthenticated = true };

            var envelope = MetadataRootCiphertextEnvelope.Parse(metadataRootEnvelope);
            var digest = SHA256.HashData(envelope.Ciphertext);
            if (!digest.AsSpan().SequenceEqual(envelope.Descriptor.MetadataCiphertextDigest)
                || !digest.AsSpan().SequenceEqual(header.MetadataRootCiphertextDigest))
            {
                return result;
            }

            result = result with { MetadataCiphertextDigestVerified = true };

            var plaintext = MetadataRootAead.AuthenticateAndDecrypt(
                header,
                envelope.Descriptor,
                vmk,
                envelope.Ciphertext,
                MetadataRootReadOnlyReader.DefaultLogicalId);
            result = result with { MetadataRootAeadAuthenticated = true };

            MetadataRootPlaintext.ValidateCanonical(plaintext, header, envelope.Descriptor);
            result = result with { MetadataPlaintextCanonicalValidated = true };

            var commitment = MetadataRootPlaintext.ComputeRootPlaintextCommitment(plaintext);
            if (!commitment.AsSpan().SequenceEqual(header.MetadataRootPlaintextCommitment))
            {
                return result;
            }

            result = result with { RootPlaintextCommitmentVerified = true };

            if (!HeaderActivationPayload.PlaintextMatchesHeader(header, activationPlain))
            {
                return result;
            }

            GenerationRollbackRules.ValidateHeaderMetadataChain(header, envelope.Descriptor);
            result = result with
            {
                GenerationRollbackValidated = true,
                Success = true
            };
            return result;
        }
        catch (UnlockValidationException)
        {
            return result;
        }
        catch (CryptographicException)
        {
            return result;
        }
    }
}