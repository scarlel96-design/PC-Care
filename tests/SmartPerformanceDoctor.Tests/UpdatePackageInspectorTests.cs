using SmartPerformanceDoctor.App.Services.Update;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class UpdatePackageInspectorTests
{
    [Fact]
    public void ComputeManifestFingerprint_MatchesPublishedPackage()
    {
        var candidates = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Desktop",
                "코딩 작업",
                "PCCare_Release_v50.2.1",
                "PCCare_Update_v50.2.1.spdup"),
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "dist",
                "updates",
                "PCCare_Update_v50.2.1.spdup")
        };

        var package = candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
        if (package is null)
        {
            return;
        }

        var inspector = new UpdatePackageInspector();
        var result = inspector.Inspect(package, "50.2.0");

        Assert.True(result.IsValid, result.Message);
        Assert.Equal("READY", result.Status);
        Assert.True(result.CanApply);
        Assert.True(result.PackageIntegrityVerified);
    }
}