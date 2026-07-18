using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.Validation;

/// <summary>Read-only XChaCha24 AEAD chain validation (structured fixture — not user vault paths).</summary>
public static class Av3XChaCha24ReadOnlyValidator
{
    public static bool ValidateActivationAndMetadataChain(
        ushort headerSuiteId,
        ushort metadataSuiteId,
        ReadOnlySpan<byte> vmk,
        VaultHeaderCopy header,
        MetadataRootDescriptor descriptor,
        ReadOnlySpan<byte> metadataCiphertext)
    {
        if (!ReadOnlyPathAllowed())
        {
            return false;
        }

        try
        {
            Av3CryptoDowngradeGuard.EnsureDecryptAllowed(headerSuiteId, metadataSuiteId, xchacha24RequiredPolicy: false);
            Av3CryptoDowngradeGuard.EnsureDecryptAllowed(header.CipherSuiteId, headerSuiteId, xchacha24RequiredPolicy: false);
            Av3CryptoDowngradeGuard.EnsureDecryptAllowed(descriptor.CipherSuiteId, metadataSuiteId, xchacha24RequiredPolicy: false);

            _ = HeaderActivationAead.AuthenticateAndDecrypt(header, vmk);
            _ = MetadataRootAead.AuthenticateAndDecrypt(
                header,
                descriptor,
                vmk,
                metadataCiphertext,
                MetadataRootReadOnlyReader.DefaultLogicalId);
            return true;
        }
        catch (UnlockValidationException)
        {
            return false;
        }
    }

    private static bool ReadOnlyPathAllowed() =>
        Av3PhaseGate.ReadOnlyValidationEnabled && !Av3PhaseGate.ProductionWriterEnabled;
}