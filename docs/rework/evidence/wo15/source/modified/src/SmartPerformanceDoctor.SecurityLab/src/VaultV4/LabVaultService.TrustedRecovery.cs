using System.Security.Cryptography;
using System.Text;
using SmartPerformanceDoctor.SecurityLab.Hardening;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

public sealed partial class LabVaultService
{
    internal FreshnessEvidence CaptureFreshnessEvidence()
    {
        using var operation = EnterOperation(); LabLocalTrustedState.ValidateFixture(_root);
        if (!IsUnlocked || _header?.Version != 6 || _selectedGeneration is null) throw new InvalidOperationException("AUTHENTICATED-V6-SESSION-REQUIRED");
        LabAuthenticatedGeneration.Verify(_root, _selectedGeneration.Activation, _vmk!);
        var generation = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{_selectedGeneration.Activation.Id}:{_selectedGeneration.Activation.HeaderHash}:{_selectedGeneration.Activation.MetadataHash}")));
        using var slots = VaultFileBoundary.OpenRead(Path.Combine(_root, "recovery"), "recovery_slots.v6.json");
        return new(_header.VaultId, _metadata!.Generation, generation, slots.Hash());
    }
    internal RecoveryReceipt UnlockWithTrustedRecovery(string code, Guid operationId, LabLocalTrustedState provider)
    {
        using var operation = EnterOperation(); LabLocalTrustedState.ValidateFixture(_root);
        if (provider.Root.Equals(_root, StringComparison.OrdinalIgnoreCase) ||
            provider.Root.StartsWith(_root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ANCHOR-MUST-BE-OUTSIDE-VAULT");
        FaultObserver?.Invoke("TrustedBeforeAuthentication");
        var header = ReadHeader();
        if (header.Version != 6) throw new InvalidOperationException("TRUSTED-RECOVERY-V6-TEST-ONLY");
        Lock();
        if (!File.Exists(Path.Combine(_root, "session.lock"))) { using var created = VaultFileBoundary.OpenWrite(_root, "session.lock", true); }
        var lease = VaultFileBoundary.OpenExclusive(_root, "session.lock", false);
        byte[]? authenticatedKey = null;
        try
        {
            if (!LabRecoverySlots.TryUnwrapVmk(_root, header.VaultId, code, out var vmk, out _, consume:false, requireAuthenticated:true) || vmk is null)
                throw new CryptographicException("RECOVERY-AUTHENTICATION-FAILED");
            authenticatedKey = vmk;
            FaultObserver?.Invoke("TrustedAfterAuthentication");
            var opened = OpenSession(vmk, header, false, "trusted_recovery_test", lease);
            if (!opened.Success) throw new CryptographicException("RECOVERY-SESSION-FAILED");
            FaultObserver?.Invoke("TrustedSessionEstablished");
            var previous = CaptureFreshnessEvidence();
            var normalized = code.Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
            var identifier = Convert.ToHexString(HMACSHA256.HashData(_vmk!, Encoding.UTF8.GetBytes("wo15/recovery-receipt/code:" + normalized)));
            provider.Reserve(previous, operationId, identifier, Guid.NewGuid());
            FaultObserver?.Invoke("TrustedBeforeConsumption");
            if (!LabRecoverySlots.TryUnwrapVmk(_root, header.VaultId, code, out var confirmed, out _, requireAuthenticated:true))
                throw new CryptographicException("RECOVERY-CONSUMPTION-FAILED");
            try { if (confirmed is null || !CryptographicOperations.FixedTimeEquals(confirmed, _vmk!)) throw new CryptographicException("RECOVERY-KEY-MISMATCH"); }
            finally { if (confirmed is not null) CryptographicOperations.ZeroMemory(confirmed); }
            FaultObserver?.Invoke("TrustedAfterConsumption");
            var receipt = provider.Commit(new(previous, CaptureFreshnessEvidence()), operationId);
            FaultObserver?.Invoke("TrustedBeforeResponse");
            FaultObserver?.Invoke("TrustedResponseReady");
            return receipt;
        }
        catch { Lock(); throw; }
        finally { if (authenticatedKey is not null) CryptographicOperations.ZeroMemory(authenticatedKey);
            if (!ReferenceEquals(_sessionLease, lease)) lease.Dispose(); }
    }
    internal RecoveryReceipt AcknowledgeTrustedRecovery(Guid operationId, LabLocalTrustedState provider)
    {
        using var operation = EnterOperation();
        if (provider.Verify(CaptureFreshnessEvidence()).Posture != RecoveryPosture.FreshnessVerified)
            throw new InvalidDataException("ACKNOWLEDGEMENT-FRESHNESS-FAILED");
        FaultObserver?.Invoke("TrustedBeforeAcknowledgement");
        var receipt = provider.Acknowledge(operationId);
        FaultObserver?.Invoke("TrustedAfterAcknowledgement"); return receipt;
    }
}
