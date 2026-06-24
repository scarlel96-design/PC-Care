using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.Aegis;

public static class AegisRecoveryCapsule
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string BuildFromLastKnownGood(AegisRecoveryManifest manifest)
    {
        AegisMirrorPaths.EnsureLayout();
        using var archiveStream = new MemoryStream();
        using (var zip = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in manifest.Files)
            {
                var relative = entry.Path.Replace('/', Path.DirectorySeparatorChar);
                var source = Path.Combine(AegisMirrorPaths.LastKnownGoodDirectory, relative);
                if (!File.Exists(source))
                {
                    continue;
                }

                var zipEntry = zip.CreateEntry(entry.Path, CompressionLevel.Optimal);
                using var entryStream = zipEntry.Open();
                using var fileStream = File.OpenRead(source);
                fileStream.CopyTo(entryStream);
            }
        }

        var plaintext = archiveStream.ToArray();
        var dek = RandomNumberGenerator.GetBytes(32);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var gcm = new AesGcm(dek, 16))
        {
            gcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        var (wrappedKey, keyMode) = AegisKeyProtector.ProtectKey(dek);
        CryptographicOperations.ZeroMemory(dek);

        var envelope = new AegisCapsuleEnvelope
        {
            Version = 1,
            Product = manifest.Product,
            ManifestVersion = manifest.Version,
            CreatedAt = DateTimeOffset.Now,
            Nonce = Convert.ToBase64String(nonce),
            Tag = Convert.ToBase64String(tag),
            WrappedKey = Convert.ToBase64String(wrappedKey),
            KeyProtectionMode = keyMode,
            Ciphertext = Convert.ToBase64String(ciphertext),
            FileCount = manifest.Files.Count
        };

        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        File.WriteAllText(AegisMirrorPaths.CapsuleFile, json, Encoding.UTF8);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    public static bool TryExtractToDirectory(string destinationDirectory, AegisRecoveryManifest manifest)
    {
        if (!File.Exists(AegisMirrorPaths.CapsuleFile))
        {
            return false;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<AegisCapsuleEnvelope>(
                File.ReadAllText(AegisMirrorPaths.CapsuleFile),
                JsonOptions);
            if (envelope is null || envelope.Version != 1)
            {
                return false;
            }

            var wrappedKey = Convert.FromBase64String(envelope.WrappedKey);
            var dek = AegisKeyProtector.UnprotectKey(wrappedKey, envelope.KeyProtectionMode);
            var nonce = Convert.FromBase64String(envelope.Nonce);
            var tag = Convert.FromBase64String(envelope.Tag);
            var ciphertext = Convert.FromBase64String(envelope.Ciphertext);
            var plaintext = new byte[ciphertext.Length];
            using (var gcm = new AesGcm(dek, 16))
            {
                gcm.Decrypt(nonce, ciphertext, tag, plaintext);
            }

            CryptographicOperations.ZeroMemory(dek);
            Directory.CreateDirectory(destinationDirectory);

            using var zipStream = new MemoryStream(plaintext);
            using var zip = new ZipArchive(zipStream, ZipArchiveMode.Read);
            foreach (var zipEntry in zip.Entries)
            {
                if (string.IsNullOrWhiteSpace(zipEntry.Name))
                {
                    continue;
                }

                var dest = Path.Combine(destinationDirectory, zipEntry.FullName.Replace('/', Path.DirectorySeparatorChar));
                var destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrWhiteSpace(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                using var entryStream = zipEntry.Open();
                using var outStream = File.Create(dest);
                entryStream.CopyTo(outStream);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool VerifyCapsuleHash(string expectedHash)
    {
        if (!File.Exists(AegisMirrorPaths.CapsuleFile) || string.IsNullOrWhiteSpace(expectedHash))
        {
            return false;
        }

        var actual = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(File.ReadAllText(AegisMirrorPaths.CapsuleFile)))).ToLowerInvariant();
        return string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class AegisCapsuleEnvelope
    {
        public int Version { get; set; }
        public string Product { get; set; } = "";
        public string ManifestVersion { get; set; } = "";
        public DateTimeOffset CreatedAt { get; set; }
        public string Nonce { get; set; } = "";
        public string Tag { get; set; } = "";
        public string WrappedKey { get; set; } = "";
        public string KeyProtectionMode { get; set; } = "dpapi-localmachine";
        public string Ciphertext { get; set; } = "";
        public int FileCount { get; set; }
    }

    public static string? ReadKeyProtectionMode()
    {
        if (!File.Exists(AegisMirrorPaths.CapsuleFile))
        {
            return null;
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<AegisCapsuleEnvelope>(
                File.ReadAllText(AegisMirrorPaths.CapsuleFile),
                JsonOptions);
            return envelope?.KeyProtectionMode;
        }
        catch
        {
            return null;
        }
    }
}