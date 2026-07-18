using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Session;
using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.Validation;

public sealed record ReadOnlyUnlockResult(
    VaultSecurityState State,
    VaultLocator Locator,
    VaultHeaderCopy SelectedHeader,
    MetadataRootDescriptor? MetadataRoot,
    MetadataRootValidationResult? MetadataValidation);

/// <summary>Phase D read-only unlock path — no writers, no persistence changes.</summary>
public static class ReadOnlyUnlockValidator
{
    public static ReadOnlyUnlockResult Validate(
        ReadOnlySpan<byte> locatorBytes,
        IReadOnlyList<(byte CopyIndex, ReadOnlyMemory<byte> CopyBytes)> headerCopies,
        byte[]? metadataRootBytes,
        string password)
    {
        if (!Av3PhaseGate.ReadOnlyValidationEnabled)
        {
            throw new InvalidOperationException("AV3 read-only validation is disabled.");
        }

        // 50.4.0: production writer may be authorized; read-only unlock remains available.

        var (locator, selected, metadataValidation) = UnlockValidationPipeline.Execute(
            locatorBytes,
            headerCopies,
            metadataRootBytes,
            password);

        // 14. No metadata graph / manifest materialization — descriptors and validation result only.
        return new ReadOnlyUnlockResult(
            VaultSecurityState.ReadOnlyUnlocked,
            locator,
            selected,
            metadataValidation?.Descriptor,
            metadataValidation);
    }
}