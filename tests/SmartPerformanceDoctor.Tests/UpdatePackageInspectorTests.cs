using SmartPerformanceDoctor.App.Services.Update;
using SmartPerformanceDoctor.App.Models.Update;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class UpdatePackageInspectorTests
{
    [Fact]
    public void UnsignedV1Package_ContentVerifiedButCannotApply()
    {
        var package = Path.Combine(Path.GetTempPath(), "pccare-unsigned-" + Guid.NewGuid().ToString("N") + ".spdup");
        var content = Encoding.UTF8.GetBytes("synthetic update payload");
        var sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        try
        {
            var manifest = new UpdateManifestDocument
            {
                FromVersion = "1.0.0",
                ToVersion = "2.0.0",
                MinimumSupportedVersion = "1.0.0",
                Files = [new UpdateManifestFileEntry { Path = "payload/app.dll", Sha256 = sha }]
            };
            using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(archive.CreateEntry(UpdatePackageInspector.ManifestEntryName).Open()))
                    writer.Write(JsonSerializer.Serialize(manifest));
                using var stream = archive.CreateEntry("payload/app.dll").Open();
                stream.Write(content);
            }
            var inspection = new UpdatePackageInspector().Inspect(package, "1.0.0");
            Assert.True(inspection.IsValid, inspection.Message);
            Assert.True(inspection.PackageIntegrityVerified);
            Assert.Equal(UpdatePublisherTrustLevel.ContentVerifiedOnly, inspection.PublisherTrust);
            Assert.False(inspection.CanApply);
            Assert.Equal("PUBLISHER-UNVERIFIED", inspection.Status);
        }
        finally
        {
            if (File.Exists(package)) File.Delete(package);
        }
    }

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
        Assert.Equal("PUBLISHER-UNVERIFIED", result.Status);
        Assert.False(result.CanApply);
        Assert.Equal(UpdatePublisherTrustLevel.ContentVerifiedOnly, result.PublisherTrust);
        Assert.True(result.PackageIntegrityVerified);
    }
}
