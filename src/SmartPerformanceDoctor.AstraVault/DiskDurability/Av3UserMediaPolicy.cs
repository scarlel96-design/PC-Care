namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>User media policy for AV3 production writer (E-14 review; not enable).</summary>
public static class Av3UserMediaPolicy
{
    public static bool NtfsLocalFixedDiskCandidate => true;

    public static bool ReFsReviewRequired => true;

    public static bool ExFatRemovableRestricted => true;

    public static bool SmbNetworkPathNoProductionWriter => true;

    public static bool CloudSyncFolderNoProductionWriter => true;

    public static bool UnknownFilesystemFailClosed => true;

    public static readonly string[] CloudSyncPathTokens =
    [
        "OneDrive",
        "Dropbox",
        "Google Drive",
        "iCloudDrive",
        "Box Sync"
    ];

    public static bool IsCloudSyncedPathSegment(string path) =>
        CloudSyncPathTokens.Any(t => path.Contains(t, StringComparison.OrdinalIgnoreCase));
}