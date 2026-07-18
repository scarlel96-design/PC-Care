using SmartPerformanceDoctor.AstraVault.Durable;

namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>E-14 harness durable write exercises (isolated temp only).</summary>
public static class Av3DiskDurabilityHarnessRunner
{
    public static Av3DurableWriteCapabilityReport RunFlushReread(string vaultRoot)
    {
        Av3DiskDurabilityHarnessScope.EnsureE14Root(vaultRoot);
        var path = Path.Combine(vaultRoot, "av3-e14-flush.bin");
        var payload = new byte[] { 0x41, 0x56, 0x33, 0x14 };
        using (var handle = new Av3DurableWriteHandle(path, "av3-e14-flush.bin"))
        {
            handle.Write(payload);
            handle.Stream.Flush(flushToDisk: true);
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                fs.Flush(flushToDisk: true);
            }
            catch
            {
                return Fail(Av3DiskDurabilityFailureReason.FlushRereadFailed, "disk_flush_failed");
            }
        }

        var read = File.ReadAllBytes(path);
        if (!read.AsSpan().SequenceEqual(payload))
        {
            return Fail(Av3DiskDurabilityFailureReason.FlushRereadFailed, "disk_reread_mismatch");
        }

        return Ok("disk_flush_reread_ok", flushReread: true);
    }

    public static Av3DurableWriteCapabilityReport RunRenameReplace(string vaultRoot)
    {
        Av3DiskDurabilityHarnessScope.EnsureE14Root(vaultRoot);
        var dir = Path.Combine(vaultRoot, "rename");
        Directory.CreateDirectory(dir);
        var temp = Path.Combine(dir, "target.tmp");
        var final = Path.Combine(dir, "target.final");
        File.WriteAllText(temp, "av3-e14-rename");
        if (File.Exists(final))
        {
            File.Delete(final);
        }

        File.Move(temp, final);
        if (!File.Exists(final) || File.Exists(temp))
        {
            return Fail(Av3DiskDurabilityFailureReason.RenameReplaceFailed, "disk_rename_failed");
        }

        return Ok("disk_rename_ok", rename: true);
    }

    public static Av3DurableWriteCapabilityReport ClassifyDirectorySync()
    {
        return new Av3DurableWriteCapabilityReport
        {
            Passed = true,
            DirectorySyncClassified = true,
            TrustedPromotionAllowed = false,
            FailureReason = Av3DiskDurabilityFailureReason.DirectorySyncUnsupported,
            PublicSummary = "disk_directory_sync_unsupported_classified"
        };
    }

    public static Av3DurableWriteCapabilityReport EvaluatePowerLossBeforeHeader()
    {
        return new Av3DurableWriteCapabilityReport
        {
            Passed = false,
            TrustedPromotionAllowed = false,
            FailureReason = Av3DiskDurabilityFailureReason.PowerLossBeforeHeaderNoPromotion,
            PublicSummary = "disk_power_loss_before_header_no_promotion"
        };
    }

    public static Av3DurableWriteCapabilityReport EvaluatePowerLossBeforeRevalidation()
    {
        return new Av3DurableWriteCapabilityReport
        {
            Passed = false,
            TrustedPromotionAllowed = false,
            FailureReason = Av3DiskDurabilityFailureReason.PowerLossBeforeRevalidationRecoveryRequired,
            PublicSummary = "disk_power_loss_before_revalidation_recovery_required"
        };
    }

    public static Av3DurableWriteCapabilityReport EvaluateCleanupFailure()
    {
        return new Av3DurableWriteCapabilityReport
        {
            Passed = false,
            TrustedPromotionAllowed = false,
            FailureReason = Av3DiskDurabilityFailureReason.CleanupFailureNoTrustedPromotion,
            PublicSummary = "disk_cleanup_failure_no_trusted_promotion"
        };
    }

    public static Av3DurableWriteCapabilityReport EvaluateStaleTemp(bool recoveredWithoutMutation)
    {
        return new Av3DurableWriteCapabilityReport
        {
            Passed = recoveredWithoutMutation,
            TrustedPromotionAllowed = false,
            FailureReason = recoveredWithoutMutation
                ? Av3DiskDurabilityFailureReason.None
                : Av3DiskDurabilityFailureReason.StaleTempRecoveryRequired,
            PublicSummary = recoveredWithoutMutation
                ? "disk_stale_temp_classified"
                : "disk_stale_temp_recovery_required"
        };
    }

    public static Av3DurableWriteCapabilityReport EvaluateSurpriseRemoval()
    {
        return new Av3DurableWriteCapabilityReport
        {
            Passed = false,
            TrustedPromotionAllowed = false,
            FailureReason = Av3DiskDurabilityFailureReason.SurpriseRemovalRecoveryRequired,
            PublicSummary = "disk_surprise_removal_recovery_required"
        };
    }

    private static Av3DurableWriteCapabilityReport Ok(string summary, bool flushReread = false, bool rename = false) =>
        new()
        {
            Passed = true,
            FlushRereadVerified = flushReread,
            RenameReplaceVerified = rename,
            TrustedPromotionAllowed = false,
            FailureReason = Av3DiskDurabilityFailureReason.None,
            PublicSummary = summary
        };

    private static Av3DurableWriteCapabilityReport Fail(Av3DiskDurabilityFailureReason reason, string summary) =>
        new()
        {
            Passed = false,
            TrustedPromotionAllowed = false,
            FailureReason = reason,
            PublicSummary = summary
        };
}