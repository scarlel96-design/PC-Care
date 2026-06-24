using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.Aegis;

public static class AegisOfflineCapsule
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string ExportLatestPack()
    {
        AegisMirrorPaths.EnsureLayout();
        Directory.CreateDirectory(AegisMirrorPaths.OfflineDirectory);

        if (!File.Exists(AegisMirrorPaths.CapsuleFile) || !File.Exists(AegisMirrorPaths.ManifestFile))
        {
            throw new InvalidOperationException("온라인 복구 캡슐 또는 매니페스트가 준비되지 않았습니다.");
        }

        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var packPath = Path.Combine(AegisMirrorPaths.OfflineDirectory, $"recovery_offline_{stamp}.aegispack");
        using var stream = File.Create(packPath);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        AddEntry(zip, "recovery.capsule", File.ReadAllBytes(AegisMirrorPaths.CapsuleFile));
        AddEntry(zip, "recovery.manifest.json", File.ReadAllBytes(AegisMirrorPaths.ManifestFile));
        if (File.Exists(AegisMirrorPaths.ManifestSignatureFile))
        {
            AddEntry(zip, "recovery.manifest.sig", File.ReadAllBytes(AegisMirrorPaths.ManifestSignatureFile));
        }

        var marker = new
        {
            product = AegisProduct.Product,
            exportedAt = DateTimeOffset.Now,
            capsuleHash = File.Exists(AegisMirrorPaths.CapsuleFile)
                ? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(AegisMirrorPaths.CapsuleFile)))).ToLowerInvariant()
                : ""
        };
        AddEntry(zip, "offline.marker.json", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(marker, JsonOptions)));
        return packPath;
    }

    public static bool TryImportPack(string packPath)
    {
        if (!File.Exists(packPath))
        {
            return false;
        }

        try
        {
            AegisMirrorPaths.EnsureLayout();
            using var stream = File.OpenRead(packPath);
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            Extract(zip, "recovery.capsule", AegisMirrorPaths.CapsuleFile);
            Extract(zip, "recovery.manifest.json", AegisMirrorPaths.ManifestFile);
            if (zip.GetEntry("recovery.manifest.sig") is not null)
            {
                Extract(zip, "recovery.manifest.sig", AegisMirrorPaths.ManifestSignatureFile);
            }

            return File.Exists(AegisMirrorPaths.CapsuleFile) && File.Exists(AegisMirrorPaths.ManifestFile);
        }
        catch
        {
            return false;
        }
    }

    public static string? LatestOfflinePackPath()
    {
        if (!Directory.Exists(AegisMirrorPaths.OfflineDirectory))
        {
            return null;
        }

        return Directory.EnumerateFiles(AegisMirrorPaths.OfflineDirectory, "recovery_offline_*.aegispack")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static void AddEntry(ZipArchive zip, string name, byte[] content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        entryStream.Write(content, 0, content.Length);
    }

    private static void Extract(ZipArchive zip, string name, string destination)
    {
        var entry = zip.GetEntry(name);
        if (entry is null)
        {
            return;
        }

        using var entryStream = entry.Open();
        using var outStream = File.Create(destination);
        entryStream.CopyTo(outStream);
    }
}