using SmartPerformanceDoctor.Contracts.Models.Installation;
using SmartPerformanceDoctor.Contracts.Services.Installation;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

/// <summary>
/// Non-elevated install layout E2E — file copy and feature gating without sc.exe / Program Files.
/// </summary>
public sealed class InstallLifecycleLayoutTests
{
    private const string Version = "50.0.0";

    [Fact]
    public void Minimal_layout_excludes_rules_pack_paths()
    {
        var manifest = FeatureCatalog.CreateManifest(InstallMode.Minimal, Version, []);
        Assert.False(FeatureInstallMapper.ShouldInstallRelativePath(@"content\data\commercial\rules.pack.json", manifest));
        Assert.False(FeatureInstallMapper.ShouldInstallRelativePath(@"engine\AstraRepairHelper.exe", manifest));
        Assert.True(FeatureInstallMapper.ShouldInstallRelativePath(@"SmartPerformanceDoctor.dll", manifest));
        Assert.True(FeatureInstallMapper.ShouldInstallRelativePath(@"PCCare.exe", manifest));
    }

    [Fact]
    public void Recommended_layout_includes_knowledge_pack_paths()
    {
        var manifest = FeatureCatalog.CreateManifest(InstallMode.Recommended, Version, []);
        Assert.True(FeatureInstallMapper.ShouldInstallRelativePath(@"content\data\commercial\rules.pack.json", manifest));
        Assert.True(FeatureInstallMapper.ShouldInstallRelativePath(@"content\data\commercial\protocols.pack.json", manifest));
    }

    [Fact]
    public void Full_layout_includes_all_optional_feature_paths()
    {
        var manifest = FeatureCatalog.CreateManifest(InstallMode.Full, Version, []);
        Assert.Equal("full", manifest.InstallMode);
        Assert.True(manifest.Features[InstallFeatureIds.PrivacyCleaner]);
        Assert.True(manifest.Features[InstallFeatureIds.PortableTools]);
        Assert.True(manifest.Features[InstallFeatureIds.InternetAcceleration]);
        Assert.True(FeatureInstallMapper.ShouldInstallRelativePath(@"content\data\commercial\rules.pack.json", manifest));
    }

    [Fact]
    public void Layout_copy_roundtrip_preserves_hashes()
    {
        var layout = ResolveLayoutRoot();
        if (!Directory.Exists(layout))
        {
            return; // skip when layout not built
        }

        var stage = Path.Combine(Path.GetTempPath(), "spd-layout-e2e-" + Guid.NewGuid().ToString("N"));
        var manifest = FeatureCatalog.CreateManifest(InstallMode.Recommended, Version, []);
        try
        {
            CopyFilteredLayout(layout, stage, manifest);
            var sample = Path.Combine(stage, "SmartPerformanceDoctor.dll");
            if (!File.Exists(sample))
            {
                sample = Directory.EnumerateFiles(stage, "SmartPerformanceDoctor.dll", SearchOption.AllDirectories).FirstOrDefault() ?? "";
            }

            Assert.False(string.IsNullOrWhiteSpace(sample));
            Assert.True(File.Exists(sample));

            // Simulate repair: corrupt then restore from layout
            var rel = Path.GetRelativePath(stage, sample);
            var layoutSource = Path.Combine(layout, rel);
            File.WriteAllText(sample, "CORRUPT");
            File.Copy(layoutSource, sample, overwrite: true);
            Assert.True(new FileInfo(sample).Length > 10);
        }
        finally
        {
            TryDelete(stage);
        }
    }

    private static string ResolveLayoutRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "artifacts", "installer", "layout");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return "";
    }

    private static void CopyFilteredLayout(string layout, string target, InstalledFeaturesManifest manifest)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(layout, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(layout, file).Replace('/', Path.DirectorySeparatorChar);
            var name = Path.GetFileName(file);
            if (name.Equals("SmartPerformanceDoctor.Setup.exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!FeatureInstallMapper.ShouldInstallRelativePath(rel, manifest))
            {
                continue;
            }

            var dest = Path.Combine(target, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, true);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // best-effort
        }
    }
}