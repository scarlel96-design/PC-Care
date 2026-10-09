using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartPerformanceDoctor.App.Models.Update;
using SmartPerformanceDoctor.App.Services.Update;

namespace SmartPerformanceDoctor.App
{
    public static class AppInfo
    {
        public const string ProductNameEnglish = "PC Care";
    }
}

internal static class Program
{
    private static int Main(string[] args)
    {
        var baseline = args.Contains("--baseline", StringComparer.Ordinal);
        var work = Path.Combine(Path.GetTempPath(), "pccare-update-inspector-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var inspector = new UpdatePackageInspector();
            var valid = InspectFixture(inspector, work, "valid", "payload/app.dll", "expected", "expected");
            var tampered = InspectFixture(inspector, work, "tampered", "payload/app.dll", "expected", "changed");
            var traversal = InspectFixture(inspector, work, "traversal", "payload/../escape.dll", "expected", "expected");
            var extra = InspectFixture(inspector, work, "extra", "payload/app.dll", "expected", "expected", "payload/extra.dll");
            var duplicate = InspectFixture(inspector, work, "duplicate", "payload/app.dll", "expected", "expected", "payload/app.dll");
            var reserved = InspectFixture(inspector, work, "reserved", "payload/CON.txt", "expected", "expected");
            Console.WriteLine($"valid={valid.Status};tampered={tampered.Status};traversal={traversal.Status};extra={extra.Status};duplicate={duplicate.Status};reserved={reserved.Status}");
            var expectedTampered = baseline ? "READY" : "CHECKSUM-FAIL";
            var expectedTraversal = baseline ? "READY" : "CHECKSUM-FAIL";
            var expectedExtra = baseline ? "READY" : "CHECKSUM-FAIL";
            return valid.Status == (baseline ? "READY" : "PUBLISHER-UNVERIFIED") && tampered.Status == expectedTampered
                && traversal.Status == expectedTraversal && extra.Status == expectedExtra
                && duplicate.Status == expectedExtra && reserved.Status == expectedExtra ? 0 : 1;
        }
        finally
        {
            Directory.Delete(work, recursive: true);
        }
    }

    private static UpdatePackageInspection InspectFixture(UpdatePackageInspector inspector, string work,
        string name, string payloadPath, string declaredContents, string actualContents, string? extraPath = null)
    {
        var archivePath = Path.Combine(work, name + ".spdup");
        var manifest = new UpdateManifestDocument
        {
            FromVersion = "1.0.0",
            ToVersion = "2.0.0",
            MinimumSupportedVersion = "1.0.0",
            Files = [new UpdateManifestFileEntry
            {
                Path = payloadPath,
                Sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(declaredContents))).ToLowerInvariant()
            }]
        };
        manifest.PackageSha256 = UpdatePackageInspector.ComputeManifestFingerprint(manifest);
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            using (var writer = new StreamWriter(archive.CreateEntry(UpdatePackageInspector.ManifestEntryName).Open()))
                writer.Write(JsonSerializer.Serialize(manifest));
            using (var writer = new StreamWriter(archive.CreateEntry(payloadPath).Open()))
                writer.Write(actualContents);
            if (extraPath is not null)
            {
                using var writer = new StreamWriter(archive.CreateEntry(extraPath).Open());
                writer.Write("extra");
            }
        }
        return inspector.Inspect(archivePath, "1.0.0");
    }
}
