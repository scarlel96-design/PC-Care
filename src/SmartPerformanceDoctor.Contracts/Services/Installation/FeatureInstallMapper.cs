using SmartPerformanceDoctor.Contracts.Models.Installation;

namespace SmartPerformanceDoctor.Contracts.Services.Installation;

public static class FeatureInstallMapper
{
    private static readonly (string FeatureId, string PathPrefix)[] OptionalPathPrefixes =
    [
        (InstallFeatureIds.DriverAudioRepair, @"engine\AstraRepairHelper.exe"),
        (InstallFeatureIds.DriverAudioRepair, @"engine\smart_performance_doctor_repair_helper.exe"),
        (InstallFeatureIds.KnowledgePack, @"content\data\commercial\rules.pack"),
        (InstallFeatureIds.DeepScanIntelligence, @"content\data\commercial\protocols.pack"),
        (InstallFeatureIds.KnowledgePack, @"content\data\commercial\security_baseline.pack"),
        (InstallFeatureIds.PortableTools, @"portable\"),
        (InstallFeatureIds.ProfessionalSecureDelete, @"content\feature-packs\professional-secure-delete\"),
        (InstallFeatureIds.SecureVault, @"content\feature-packs\secure-vault\"),
        (InstallFeatureIds.SystemCare, @"content\feature-packs\system-care\"),
        (InstallFeatureIds.RegistryDoctor, @"content\feature-packs\registry-doctor\"),
        (InstallFeatureIds.DiskDoctor, @"content\feature-packs\disk-doctor\"),
        (InstallFeatureIds.VulnerabilityFix, @"content\feature-packs\vulnerability-fix\"),
        (InstallFeatureIds.InternetAcceleration, @"content\feature-packs\internet-acceleration\"),
    ];

    public static bool ShouldInstallRelativePath(string relativePath, InstalledFeaturesManifest manifest)
    {
        var normalized = relativePath.Replace('/', '\\');
        foreach (var (featureId, prefix) in OptionalPathPrefixes)
        {
            if (!MatchesPrefix(normalized, prefix))
            {
                continue;
            }

            if (featureId is InstallFeatureIds.KnowledgePack or InstallFeatureIds.DeepScanIntelligence)
            {
                return manifest.IsEnabled(InstallFeatureIds.KnowledgePack)
                    || manifest.IsEnabled(InstallFeatureIds.DeepScanIntelligence);
            }

            return manifest.IsEnabled(featureId);
        }

        return true;
    }

    public static IReadOnlyList<string> GetSkippedPaths(InstalledFeaturesManifest manifest, IEnumerable<string> relativePaths) =>
        relativePaths
            .Where(path => !ShouldInstallRelativePath(path, manifest))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static bool MatchesPrefix(string normalizedRelativePath, string prefix)
    {
        if (prefix.EndsWith('\\'))
        {
            return normalizedRelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return normalizedRelativePath.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || normalizedRelativePath.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase);
    }
}