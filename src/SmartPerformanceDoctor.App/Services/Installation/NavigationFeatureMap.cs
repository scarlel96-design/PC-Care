using SmartPerformanceDoctor.Contracts.Models.Installation;

namespace SmartPerformanceDoctor.App.Services.Installation;

public static class NavigationFeatureMap
{
    public static string? ResolveFeatureId(string? navTag)
    {
        if (string.IsNullOrWhiteSpace(navTag) || !navTag.StartsWith("feature:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return navTag["feature:".Length..];
    }

    public static bool ShouldShow(string? featureId, InstalledFeaturesService features)
    {
        if (string.IsNullOrWhiteSpace(featureId))
        {
            return true;
        }

        if (features.IsEnabled(featureId))
        {
            return true;
        }

        return featureId switch
        {
            InstallFeatureIds.SystemCare => features.IsAnyEnabled(
                InstallFeatureIds.RegistryDoctor,
                InstallFeatureIds.DiskDoctor,
                InstallFeatureIds.PrivacyCleaner,
                InstallFeatureIds.JunkCleaner,
                InstallFeatureIds.ShortcutRepair),
            _ => false
        };
    }
}