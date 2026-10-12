namespace SmartPerformanceDoctor.SecurityLab.ProductBridge;

/// <summary>
/// Historical package metadata. Production approval is evaluated separately.
/// </summary>
public static class LabReleaseState
{
    public const string ProductVersion = "50.4.0";
    public const string SetupFileName = "PCCare_Setup_v50.4.0.exe";
    public const string SetupRelativePath = "artifacts/installer/setup/PCCare_Setup_v50.4.0.exe";
    public const string UpdateFileName = "PCCare_Update_v50.4.0.spdup";

    public const bool InstallerPackageReleased = true;
    public const bool LabDesignTrackComplete = true;

    /// <summary>AV3 production approval remains closed.</summary>
    public const bool Av3GatesAuthorized = false;

    public static string Summary =>
        $"LabRelease v{ProductVersion} · PackageReleased={InstallerPackageReleased} · " +
        $"DesignComplete={LabDesignTrackComplete} · Setup={SetupFileName} · " +
        $"Update={UpdateFileName} · AV3.Writer={(Av3GateSnapshot.ProductionWriterEnabled ? "ON" : "OFF")}";
}
