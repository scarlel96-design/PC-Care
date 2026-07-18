namespace SmartPerformanceDoctor.AstraVault.Legacy;

public enum DetectedVaultKind
{
    None,
    SpdLegacy,
    AstraV3
}

public static class LegacyVaultInventory
{
    public const string SpdMarkerFile = "vault.svdb";
    public const string AstraLocatorFile = "vault.locator";

    public static DetectedVaultKind Detect(string vaultRoot)
    {
        if (string.IsNullOrWhiteSpace(vaultRoot) || !Directory.Exists(vaultRoot))
        {
            return DetectedVaultKind.None;
        }

        if (File.Exists(Path.Combine(vaultRoot, AstraLocatorFile)))
        {
            return DetectedVaultKind.AstraV3;
        }

        if (File.Exists(Path.Combine(vaultRoot, SpdMarkerFile)))
        {
            return DetectedVaultKind.SpdLegacy;
        }

        return DetectedVaultKind.None;
    }
}