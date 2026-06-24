using SmartPerformanceDoctor.Contracts.Models.Installation;
using SmartPerformanceDoctor.Contracts.Services.Installation;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class FeatureInstallMapperTests
{
    [Fact]
    public void Minimal_install_skips_optional_engine_and_pack_files()
    {
        var manifest = new InstalledFeaturesManifest
        {
            InstallMode = "minimal",
            Features = InstallFeatureIds.Required.ToDictionary(id => id, _ => true)
        };

        Assert.False(FeatureInstallMapper.ShouldInstallRelativePath(@"engine\AstraRepairHelper.exe", manifest));
        Assert.False(FeatureInstallMapper.ShouldInstallRelativePath(@"content\data\commercial\rules.pack.json", manifest));
        Assert.False(FeatureInstallMapper.ShouldInstallRelativePath(@"content\data\commercial\protocols.pack.json.sig", manifest));
        Assert.True(FeatureInstallMapper.ShouldInstallRelativePath(@"SmartPerformanceDoctor.dll", manifest));
    }

    [Fact]
    public void Knowledge_pack_enables_commercial_rule_and_protocol_files()
    {
        var manifest = new InstalledFeaturesManifest
        {
            InstallMode = "recommended",
            Features = InstallFeatureIds.Required.ToDictionary(id => id, _ => true)
        };
        manifest.Features[InstallFeatureIds.KnowledgePack] = true;

        Assert.True(FeatureInstallMapper.ShouldInstallRelativePath(@"content\data\commercial\rules.pack.json", manifest));
        Assert.True(FeatureInstallMapper.ShouldInstallRelativePath(@"content\data\commercial\protocols.pack.json", manifest));
    }

    [Fact]
    public void GetSkippedPaths_reports_only_blocked_optional_files()
    {
        var manifest = new InstalledFeaturesManifest
        {
            Features = InstallFeatureIds.Required.ToDictionary(id => id, _ => true)
        };

        var skipped = FeatureInstallMapper.GetSkippedPaths(
            manifest,
            [
                @"SmartPerformanceDoctor.dll",
                @"engine\AstraRepairHelper.exe",
                @"content\data\commercial\rules.pack.json"
            ]);

        Assert.Equal(2, skipped.Count);
        Assert.Contains(@"engine\AstraRepairHelper.exe", skipped);
        Assert.Contains(@"content\data\commercial\rules.pack.json", skipped);
    }
}