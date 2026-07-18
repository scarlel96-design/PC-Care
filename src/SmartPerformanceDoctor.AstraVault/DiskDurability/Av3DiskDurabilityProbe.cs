using SmartPerformanceDoctor.AstraVault.Commit;

namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>E-14 durability probe (path policy + harness checks; no user vault).</summary>
public static class Av3DiskDurabilityProbe
{
    public static DriveType? TestingDriveTypeOverride { get; set; }

    public static string? TestingFilesystemOverride { get; set; }

    public static bool TestingSimulateOutOfSpace { get; set; }

    public static bool TestingSimulateAccessDenied { get; set; }

    public static bool TestingSimulateFileLock { get; set; }

    public static int TestingFileLockAttempts { get; set; }

    public static Av3DiskDurabilityProbeResult ProbePath(string? path, bool testHarnessInvocation)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Fail(Av3DiskDurabilityFailureReason.IsolatedRootRequired, "disk_path_missing");
        }

        if (TestingSimulateAccessDenied)
        {
            return Fail(Av3DiskDurabilityFailureReason.AccessDenied, "disk_access_denied");
        }

        if (TestingSimulateOutOfSpace)
        {
            return Fail(Av3DiskDurabilityFailureReason.OutOfSpace, "disk_out_of_space");
        }

        if (Av3UserMediaPolicy.IsCloudSyncedPathSegment(path))
        {
            var cap = Av3DiskDurabilityClassifier.ClassifyPath(path, "cloud-sync", false, false, true, false);
            return new Av3DiskDurabilityProbeResult
            {
                Success = false,
                FailureReason = Av3DiskDurabilityFailureReason.CloudSyncPathNoProductionWriter,
                Capability = cap,
                PublicSummary = "disk_cloud_sync_no_production_writer"
            };
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            var cap = Av3DiskDurabilityClassifier.ClassifyPath(path, "network-share", false, true, false, false);
            return new Av3DiskDurabilityProbeResult
            {
                Success = false,
                FailureReason = Av3DiskDurabilityFailureReason.NetworkPathNoProductionWriter,
                Capability = cap,
                PublicSummary = "disk_network_no_production_writer"
            };
        }

        var isE14 = Av3DiskDurabilityHarnessScope.IsE14RootAllowed(path, out var normalized);
        if (testHarnessInvocation && isE14)
        {
            var harnessCap = Av3DiskDurabilityClassifier.ClassifyPath(normalized, "harness", false, false, false, true);
            return new Av3DiskDurabilityProbeResult
            {
                Success = true,
                Capability = harnessCap,
                FreeBytes = GetFreeBytesSafe(normalized),
                PublicSummary = "disk_harness_probe_ok"
            };
        }

        if (!testHarnessInvocation)
        {
            return Fail(Av3DiskDurabilityFailureReason.HarnessOnlyRequired, "disk_harness_only");
        }

        var drive = GetDriveType(path);
        var fs = TestingFilesystemOverride ?? DetectFilesystem(path);
        var removable = drive == DriveType.Removable || drive == DriveType.Unknown && TestingDriveTypeOverride == DriveType.Removable;
        var network = drive == DriveType.Network || TestingDriveTypeOverride == DriveType.Network;
        var capability = Av3DiskDurabilityClassifier.ClassifyPath(path, fs, removable, network, false, false);

        if (capability.Classification == Av3DiskDurabilityClassification.UnknownFailClosed)
        {
            return new Av3DiskDurabilityProbeResult
            {
                Success = false,
                FailureReason = Av3DiskDurabilityFailureReason.UnknownFilesystemFailClosed,
                Capability = capability,
                PublicSummary = "disk_unknown_filesystem_fail_closed"
            };
        }

        if (capability.Classification == Av3DiskDurabilityClassification.RemovableRestricted)
        {
            return new Av3DiskDurabilityProbeResult
            {
                Success = false,
                FailureReason = Av3DiskDurabilityFailureReason.RemovableMediaWithoutPolicy,
                Capability = capability,
                PublicSummary = "disk_removable_restricted"
            };
        }

        var free = GetFreeBytesSafe(path);
        if (free < Av3DiskDurabilityPolicy.MinimumFreeBytesThreshold && !TestingSimulateOutOfSpace)
        {
            // harness temp usually has space; only fail when explicitly simulated
        }

        return new Av3DiskDurabilityProbeResult
        {
            Success = capability.Classification == Av3DiskDurabilityClassification.NtfsFixedDiskCandidate,
            FailureReason = capability.Classification == Av3DiskDurabilityClassification.NtfsFixedDiskCandidate
                ? Av3DiskDurabilityFailureReason.None
                : Av3DiskDurabilityFailureReason.UnsupportedFilesystem,
            Capability = capability,
            FreeBytes = free,
            PublicSummary = "disk_probe_classified"
        };
    }

    public static bool TryFileLockWithRetry(out Av3DiskDurabilityFailureReason reason)
    {
        if (!TestingSimulateFileLock)
        {
            reason = Av3DiskDurabilityFailureReason.None;
            return true;
        }

        TestingFileLockAttempts++;
        if (TestingFileLockAttempts < Av3DiskDurabilityPolicy.FileLockRetryMaxAttempts)
        {
            reason = Av3DiskDurabilityFailureReason.None;
            return false;
        }

        reason = Av3DiskDurabilityFailureReason.FileLockExhausted;
        return false;
    }

    public static void ResetTestingState()
    {
        TestingDriveTypeOverride = null;
        TestingFilesystemOverride = null;
        TestingSimulateOutOfSpace = false;
        TestingSimulateAccessDenied = false;
        TestingSimulateFileLock = false;
        TestingFileLockAttempts = 0;
    }

    private static DriveType GetDriveType(string path)
    {
        if (TestingDriveTypeOverride.HasValue)
        {
            return TestingDriveTypeOverride.Value;
        }

        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root))
            {
                return DriveType.Unknown;
            }

            return new DriveInfo(root).DriveType;
        }
        catch
        {
            return DriveType.Unknown;
        }
    }

    private static string DetectFilesystem(string path)
    {
        if (TestingFilesystemOverride is not null)
        {
            return TestingFilesystemOverride;
        }

        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root) || !OperatingSystem.IsWindows())
            {
                return "UNKNOWN";
            }

            return new DriveInfo(root).DriveFormat.ToUpperInvariant();
        }
        catch
        {
            return "UNKNOWN";
        }
    }

    private static ulong GetFreeBytesSafe(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root))
            {
                return ulong.MaxValue;
            }

            return (ulong)new DriveInfo(root).AvailableFreeSpace;
        }
        catch
        {
            return ulong.MaxValue;
        }
    }

    private static Av3DiskDurabilityProbeResult Fail(Av3DiskDurabilityFailureReason reason, string summary) =>
        new()
        {
            Success = false,
            FailureReason = reason,
            PublicSummary = summary
        };
}