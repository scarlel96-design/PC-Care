using System.Security.Cryptography;
using System.Text.Json;

namespace SmartPerformanceDoctor.Aegis;

public sealed class AegisIntegrityVerifier
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public IReadOnlyList<AegisIntegrityFinding> VerifyAgainstManifest(AegisRecoveryManifest manifest)
    {
        var findings = new List<AegisIntegrityFinding>();
        var root = AegisRuntimeContext.InstallRoot;

        foreach (var entry in manifest.Files)
        {
            var fullPath = Path.Combine(root, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
            {
                findings.Add(new AegisIntegrityFinding
                {
                    RelativePath = entry.Path,
                    Tier = entry.Tier,
                    Reason = "파일 누락",
                    Repairable = File.Exists(Path.Combine(AegisMirrorPaths.LastKnownGoodDirectory, entry.Path.Replace('/', Path.DirectorySeparatorChar)))
                });
                continue;
            }

            var info = new FileInfo(fullPath);
            if (info.Length != entry.Size)
            {
                findings.Add(new AegisIntegrityFinding
                {
                    RelativePath = entry.Path,
                    Tier = entry.Tier,
                    Reason = $"크기 불일치 (기대 {entry.Size}, 실제 {info.Length})",
                    Repairable = true
                });
            }

            var hash = ComputeSha256(fullPath);
            if (!string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new AegisIntegrityFinding
                {
                    RelativePath = entry.Path,
                    Tier = entry.Tier,
                    Reason = "SHA256 불일치",
                    Repairable = true
                });
            }
        }

        return findings;
    }

    public AegisRecoveryManifest BuildBaselineManifest(string version)
    {
        var entries = new List<AegisRecoveryManifestEntry>();
        foreach (var (relative, tier) in EnumerateProtectedFiles())
        {
            var fullPath = Path.Combine(AegisRuntimeContext.InstallRoot, relative);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var info = new FileInfo(fullPath);
            entries.Add(new AegisRecoveryManifestEntry
            {
                Path = relative.Replace(Path.DirectorySeparatorChar, '/'),
                Tier = tier,
                Size = info.Length,
                Sha256 = ComputeSha256(fullPath),
                SignatureRequired = tier is "app-critical" or "recovery-critical"
            });
        }

        return new AegisRecoveryManifest
        {
            Product = AegisProduct.Product,
            Version = version,
            CreatedAt = DateTimeOffset.Now,
            Files = entries,
            CapsuleVersion = "1"
        };
    }

    public void SaveManifest(AegisRecoveryManifest manifest, string? capsuleHash = null)
    {
        AegisMirrorPaths.EnsureLayout();
        if (!string.IsNullOrWhiteSpace(capsuleHash))
        {
            manifest.CapsuleHash = capsuleHash;
        }

        var signingJson = AegisManifestSigner.SerializeForSigning(manifest);
        File.WriteAllText(AegisMirrorPaths.ManifestFile, JsonSerializer.Serialize(manifest, JsonOptions));
        if (!AegisManifestSigner.TryWriteSignature(signingJson, AegisMirrorPaths.ManifestSignatureFile))
        {
            // Runtime must not embed a private key. Unsigned manifests fail verification until CI signs them.
            try
            {
                if (File.Exists(AegisMirrorPaths.ManifestSignatureFile))
                {
                    File.Delete(AegisMirrorPaths.ManifestSignatureFile);
                }
            }
            catch
            {
                // Best-effort cleanup of stale signature.
            }
        }

        File.Copy(AegisMirrorPaths.ManifestFile, Path.Combine(AegisMirrorPaths.Root, "recovery.manifest.backup.json"), overwrite: true);
    }

    public (AegisRecoveryManifest? Manifest, bool SignatureValid) TryLoadSignedManifest()
    {
        if (!File.Exists(AegisMirrorPaths.ManifestFile))
        {
            return (null, false);
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<AegisRecoveryManifest>(
                File.ReadAllText(AegisMirrorPaths.ManifestFile),
                JsonOptions);
            if (manifest is null)
            {
                return (null, false);
            }

            if (!File.Exists(AegisMirrorPaths.ManifestSignatureFile))
            {
                return (manifest, false);
            }

            var signingJson = AegisManifestSigner.SerializeForSigning(manifest);
            var signature = File.ReadAllText(AegisMirrorPaths.ManifestSignatureFile).Trim();
            var valid = AegisManifestSigner.VerifyManifestJson(signingJson, signature);
            return (manifest, valid);
        }
        catch
        {
            return (null, false);
        }
    }

    public AegisRecoveryManifest? TryLoadManifest() => TryLoadSignedManifest().Manifest;

    public void SnapshotLastKnownGood(AegisRecoveryManifest manifest)
    {
        AegisMirrorPaths.EnsureLayout();
        foreach (var entry in manifest.Files)
        {
            var source = Path.Combine(AegisRuntimeContext.InstallRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(source))
            {
                continue;
            }

            var dest = Path.Combine(AegisMirrorPaths.LastKnownGoodDirectory, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            var destDir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrWhiteSpace(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(source, dest, overwrite: true);
        }
    }

    public int TryRestoreFromLastKnownGood(IReadOnlyList<AegisIntegrityFinding> findings)
    {
        var restored = 0;
        foreach (var finding in findings.Where(f => f.Repairable))
        {
            var relative = finding.RelativePath.Replace('/', Path.DirectorySeparatorChar);
            var backup = Path.Combine(AegisMirrorPaths.LastKnownGoodDirectory, relative);
            if (!File.Exists(backup))
            {
                continue;
            }

            var target = Path.Combine(AegisRuntimeContext.InstallRoot, relative);
            var targetDir = Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            QuarantineIfExists(target, finding.Reason);
            File.Copy(backup, target, overwrite: true);
            restored++;
        }

        return restored;
    }

    private static void QuarantineIfExists(string targetPath, string reason)
    {
        if (!File.Exists(targetPath))
        {
            return;
        }

        AegisMirrorPaths.EnsureLayout();
        var name = $"{DateTimeOffset.Now:yyyyMMddHHmmss}_{Path.GetFileName(targetPath)}_{Sanitize(reason)}.quarantine";
        var quarantinePath = Path.Combine(AegisMirrorPaths.QuarantineDirectory, name);
        try
        {
            File.Move(targetPath, quarantinePath, overwrite: true);
        }
        catch
        {
            // Best-effort quarantine.
        }
    }

    private static string Sanitize(string value) =>
        string.Concat(value.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim('_');

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static IEnumerable<(string Relative, string Tier)> EnumerateProtectedFiles()
    {
        yield return (AegisProduct.BrandedExePreferred, "app-critical");
        yield return (AegisProduct.MainExe, "app-critical");
        yield return ("SmartPerformanceDoctor.dll", "app-critical");

        var core = AegisRuntimeContext.ResolveCoreEnginePath();
        if (File.Exists(core))
        {
            var relative = Path.GetRelativePath(AegisRuntimeContext.InstallRoot, core);
            yield return (relative, "app-critical");
        }

        var helper = AegisRuntimeContext.ResolveRepairHelperPath();
        if (File.Exists(helper))
        {
            var relative = Path.GetRelativePath(AegisRuntimeContext.InstallRoot, helper);
            yield return (relative, "app-critical");
        }

        foreach (var recoveryName in new[] { AegisProduct.RecoveryHelperExe, AegisProduct.RecoveryServiceExe })
        {
            var recoveryPath = Path.Combine(AegisRuntimeContext.EngineDirectory, recoveryName);
            if (File.Exists(recoveryPath))
            {
                yield return (Path.GetRelativePath(AegisRuntimeContext.InstallRoot, recoveryPath), "recovery-critical");
            }
        }

        foreach (var file in SafeEnumerateFiles(AegisRuntimeContext.RulesDirectory))
        {
            yield return (Path.GetRelativePath(AegisRuntimeContext.InstallRoot, file), "intelligence-critical");
        }

        var commercial = AegisRuntimeContext.CommercialDataDirectory;
        foreach (var file in SafeEnumerateFiles(commercial))
        {
            yield return (Path.GetRelativePath(AegisRuntimeContext.InstallRoot, file), "intelligence-critical");
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            yield return file;
        }
    }
}