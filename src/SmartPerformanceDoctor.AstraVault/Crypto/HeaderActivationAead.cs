using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Validation;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>AEAD authentication path for header activation payload (post-VMK unwrap).</summary>
public static class HeaderActivationAead
{
    public const string ActivationKeyDomain = "astra-header-activation";

    public static byte[] DeriveActivationKey(ReadOnlySpan<byte> vmk) =>
        AstraKdf.DeriveDomainKey(vmk, ReadOnlySpan<byte>.Empty, ActivationKeyDomain);

    public static byte[] AuthenticateAndDecrypt(VaultHeaderCopy copy, ReadOnlySpan<byte> vmk)
    {
        byte[]? activationKey = null;
        try
        {
            activationKey = DeriveActivationKey(vmk);
            var aad = ActivationPayloadAad.Build(
                AstraFormatConstants.MajorVersion,
                copy.ContainerId,
                copy.VaultId,
                copy.CopyIndex,
                copy.Generation,
                copy.CipherSuiteId,
                copy.MetadataRootCiphertextDigest,
                copy.ActivationTarget);

            Av3CryptoDowngradeGuard.EnsureDecryptAllowed(copy.CipherSuiteId, copy.CipherSuiteId, xchacha24RequiredPolicy: false);
            var suite = Av3AeadDispatch.ToCipherSuite(copy.CipherSuiteId);
            var blob = new AstraCiphertext(copy.ActivationNonce, copy.ActivationTag, copy.ActivationCiphertext);
            var plain = AstraAead.Decrypt(suite, activationKey, blob, aad);
            if (plain.Length != HeaderActivationPayload.PlaintextSize)
            {
                throw new UnlockValidationException();
            }

            if (!HeaderActivationPayload.PlaintextMatchesHeader(copy, plain))
            {
                throw new UnlockValidationException();
            }

            if (!HeaderActivationPayload.DigestFromPlaintext(plain).AsSpan().SequenceEqual(copy.ActivationPayloadDigest))
            {
                throw new UnlockValidationException();
            }

            return plain;
        }
        catch (UnlockValidationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new UnlockValidationException();
        }
        finally
        {
            AstraKdf.Zero(activationKey);
        }
    }
}