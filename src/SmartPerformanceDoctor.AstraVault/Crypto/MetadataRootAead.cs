using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Validation;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>AEAD authentication path for metadata-root ciphertext (post-activation).</summary>
public static class MetadataRootAead
{
    public const string MetadataKeyDomain = MetadataRootAad.DomainLabel;

    public static byte[] DeriveMetadataKey(ReadOnlySpan<byte> vmk) =>
        AstraKdf.DeriveDomainKey(vmk, ReadOnlySpan<byte>.Empty, MetadataKeyDomain);

    public static byte[] AuthenticateAndDecrypt(
        VaultHeaderCopy header,
        MetadataRootDescriptor descriptor,
        ReadOnlySpan<byte> vmk,
        ReadOnlySpan<byte> ciphertext,
        ulong metadataRootLogicalId)
    {
        if (ciphertext.Length != descriptor.CiphertextLength)
        {
            throw new UnlockValidationException();
        }

        byte[]? metadataKey = null;
        try
        {
            metadataKey = DeriveMetadataKey(vmk);
            var aad = MetadataRootAad.Build(
                AstraFormatConstants.MajorVersion,
                descriptor.CipherSuiteId,
                descriptor.ContainerId,
                header.VaultId,
                header.Generation,
                descriptor.Generation,
                descriptor.MetadataCiphertextDigest,
                header.ActivationPayloadDigest,
                metadataRootLogicalId,
                descriptor.CiphertextLength);

            Av3CryptoDowngradeGuard.EnsureDecryptAllowed(descriptor.CipherSuiteId, descriptor.CipherSuiteId, xchacha24RequiredPolicy: false);
            Av3CryptoDowngradeGuard.EnsureDecryptAllowed(header.CipherSuiteId, descriptor.CipherSuiteId, xchacha24RequiredPolicy: false);
            var suite = Av3AeadDispatch.ToCipherSuite(descriptor.CipherSuiteId);
            var blob = new AstraCiphertext(descriptor.Nonce, descriptor.Tag, ciphertext.ToArray());
            var plain = AstraAead.Decrypt(suite, metadataKey, blob, aad);
            if (plain.Length != MetadataRootPlaintext.PlaintextSize)
            {
                throw new UnlockValidationException();
            }

            MetadataRootPlaintext.ValidateCanonical(plain, header, descriptor);
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
            AstraKdf.Zero(metadataKey);
        }
    }
}