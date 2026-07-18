namespace SmartPerformanceDoctor.SecurityLab.ProductBridge;

/// <summary>
/// 50.4.0 product complete: Setup + Lab design 100% + AV3 gates authorized.
/// </summary>
public static class LabReleaseState
{
    public const string ProductVersion = "50.4.0";
    public const string SetupFileName = "PCCare_Setup_v50.4.0.exe";
    public const string SetupRelativePath = "artifacts/installer/setup/PCCare_Setup_v50.4.0.exe";
    public const string UpdateFileName = "PCCare_Update_v50.4.0.spdup";

    public const bool InstallerPackageReleased = true;
    public const bool LabDesignTrackComplete = true;

    /// <summary>AV3 production gates authorized (50.4.0 product GO). Lab remains active vault path.</summary>
    public const bool Av3GatesAuthorized = true;

    public static string Summary =>
        $"LabRelease v{ProductVersion} · PackageReleased={InstallerPackageReleased} · " +
        $"DesignComplete={LabDesignTrackComplete} · Setup={SetupFileName} · " +
        $"Update={UpdateFileName} · AV3.Writer={(Av3GateSnapshot.ProductionWriterEnabled ? "ON" : "OFF")}";
}
