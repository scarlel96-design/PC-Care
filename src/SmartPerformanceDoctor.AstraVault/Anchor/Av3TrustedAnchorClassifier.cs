using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Classifies external/hybrid witness evidence for full-vault rollback posture.</summary>
public static class Av3TrustedAnchorClassifier
{
    public static Av3TrustedAnchorVerification VerifyExternalWitness(
        ulong observedVaultGeneration,
        ulong externalCounter,
        string expectedDigestHex,
        string witnessDigestHex,
        bool signatureValid,
        bool replayDetected,
        bool serverAvailable,
        Av3TrustedAnchorOfflineState offlineState)
    {
        if (!serverAvailable)
        {
            return Fail(
                Av3TrustedAnchorFailureReason.ExternalWitnessUnavailable,
                Av3AnchorStatus.AnchorUnavailable,
                fullRollback: false,
                productionEnable: false,
                writerPromotion: false,
                "trusted_external_witness_unavailable");
        }

        if (replayDetected)
        {
            return Fail(
                Av3TrustedAnchorFailureReason.ExternalWitnessReplayDetected,
                Av3AnchorStatus.AnchorRecoveryRequired,
                fullRollback: true,
                productionEnable: false,
                writerPromotion: false,
                "trusted_external_witness_replay");
        }

        if (!signatureValid)
        {
            return Fail(
                Av3TrustedAnchorFailureReason.ExternalWitnessSignatureInvalid,
                Av3AnchorStatus.AnchorRecoveryRequired,
                fullRollback: false,
                productionEnable: false,
                writerPromotion: false,
                "trusted_external_witness_signature_invalid");
        }

        if (!string.Equals(expectedDigestHex, witnessDigestHex, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(
                Av3TrustedAnchorFailureReason.ExternalWitnessDigestMismatch,
                Av3AnchorStatus.AnchorRollbackSuspected,
                fullRollback: true,
                productionEnable: false,
                writerPromotion: false,
                "trusted_external_witness_digest_mismatch");
        }

        if (externalCounter > observedVaultGeneration)
        {
            return Fail(
                Av3TrustedAnchorFailureReason.ExternalWitnessCounterRollback,
                Av3AnchorStatus.AnchorRollbackSuspected,
                fullRollback: true,
                productionEnable: false,
                writerPromotion: false,
                "trusted_external_witness_counter_higher");
        }

        if (externalCounter < observedVaultGeneration)
        {
            return Fail(
                Av3TrustedAnchorFailureReason.ExternalWitnessCounterStale,
                Av3AnchorStatus.AnchorStale,
                fullRollback: false,
                productionEnable: false,
                writerPromotion: false,
                "trusted_external_witness_counter_lower");
        }

        var writerPromotion = Av3TrustedAnchorOfflinePolicy.AllowsWriterTrustedPromotion(offlineState);
        return new Av3TrustedAnchorVerification
        {
            Verified = true,
            AnchorStatus = Av3AnchorStatus.AnchorFresh,
            FailureReason = Av3TrustedAnchorFailureReason.None,
            FullVaultRollbackSuspected = false,
            ProductionEnableAllowed = writerPromotion && serverAvailable,
            WriterTrustedPromotionAllowed = writerPromotion,
            PublicSummary = "trusted_external_witness_fresh"
        };
    }

    public static bool SameDiskAnchorCanCloseFullVaultRollback(Av3TrustedAnchorProviderKind kind) =>
        kind != Av3TrustedAnchorProviderKind.SameDiskLocalUntrusted
        && kind != Av3TrustedAnchorProviderKind.HarnessSynthetic;

    public static Av3TrustedAnchorVerification ClassifySameDiskOnlyPosture()
    {
        return Fail(
            Av3TrustedAnchorFailureReason.SameDiskAnchorInsufficientForFullRollback,
            Av3AnchorStatus.AnchorFresh,
            fullRollback: false,
            productionEnable: false,
            writerPromotion: false,
            "trusted_same_disk_cannot_close_full_rollback");
    }

    private static Av3TrustedAnchorVerification Fail(
        Av3TrustedAnchorFailureReason reason,
        Av3AnchorStatus status,
        bool fullRollback,
        bool productionEnable,
        bool writerPromotion,
        string summary) =>
        new()
        {
            Verified = false,
            AnchorStatus = status,
            FailureReason = reason,
            FullVaultRollbackSuspected = fullRollback,
            ProductionEnableAllowed = productionEnable,
            WriterTrustedPromotionAllowed = writerPromotion,
            PublicSummary = summary
        };
}