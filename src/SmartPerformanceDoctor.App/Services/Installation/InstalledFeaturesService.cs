using System.Text.Json;
using SmartPerformanceDoctor.Contracts.Models.Installation;
using SmartPerformanceDoctor.Contracts.Services.Installation;

namespace SmartPerformanceDoctor.App.Services.Installation;

public sealed class InstalledFeaturesService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private InstalledFeaturesManifest? _cache;

    public IReadOnlyList<string> ManifestSearchPaths => BuildManifestSearchPaths();

    public string ManifestPath => ManifestSearchPaths[0];

    public string LocalFallbackPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PCCare",
        "installed_features.json");

    public InstalledFeaturesManifest Load()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        foreach (var path in ManifestSearchPaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(path);
                var manifest = JsonSerializer.Deserialize<InstalledFeaturesManifest>(json, JsonOptions)
                    ?? CreateRuntimeDefault();
                manifest = UpgradeIfNeeded(manifest);
                _cache = manifest;
                return _cache;
            }
            catch
            {
                // try next path
            }
        }

        _cache = CreateRuntimeDefault();
        return _cache;
    }

    public bool IsEnabled(string featureId) => Load().IsEnabled(featureId);

    public bool IsAnyEnabled(params string[] featureIds) =>
        featureIds.Any(IsEnabled);

    public void Save(InstalledFeaturesManifest manifest)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(ManifestPath, json);
        Directory.CreateDirectory(Path.GetDirectoryName(LocalFallbackPath)!);
        File.WriteAllText(LocalFallbackPath, json);
        _cache = manifest;
    }

    public void Refresh() => _cache = null;

    private static InstalledFeaturesManifest CreateRuntimeDefault()
    {
        var manifest = FeatureCatalog.CreateRuntimeDefault(AppInfo.BuildVersion);
        FeatureCatalog.EnsureV49PrimaryFeatures(manifest);
        return manifest;
    }

    private static InstalledFeaturesManifest UpgradeIfNeeded(InstalledFeaturesManifest manifest)
    {
        var needsUpgrade = !manifest.IsEnabled(InstallFeatureIds.SystemCare)
            || !manifest.IsEnabled(InstallFeatureIds.SecureVault)
            || !manifest.IsEnabled(InstallFeatureIds.ProfessionalSecureDelete);

        if (!needsUpgrade)
        {
            return manifest;
        }

        FeatureCatalog.EnsureV49PrimaryFeatures(manifest);
        manifest.Version = AppInfo.BuildVersion;
        manifest.InstallMode = string.IsNullOrWhiteSpace(manifest.InstallMode) ? "recommended" : manifest.InstallMode;
        return manifest;
    }

    private static IReadOnlyList<string> BuildManifestSearchPaths()
    {
        var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return
        [
            Path.Combine(common, "PCCare", "installed_features.json"),
            Path.Combine(common, "AstraCare", "installed_features.json"),
            Path.Combine(common, "SmartPerformanceDoctor", "installed_features.json"),
            Path.Combine(local, "PCCare", "installed_features.json"),
            Path.Combine(local, "AstraCare", "installed_features.json"),
            Path.Combine(local, "SmartPerformanceDoctor", "installed_features.json")
        ];
    }
}