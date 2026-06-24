using System.Text.Json;

namespace SmartPerformanceDoctor.Aegis;

public static class AegisManifestQuorum
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static (AegisRecoveryManifest? Manifest, bool SignatureValid, string Source) TryLoadWithQuorum()
    {
        var verifier = new AegisIntegrityVerifier();
        var (primary, primaryValid) = verifier.TryLoadSignedManifest();
        if (primary is not null && primaryValid)
        {
            return (primary, true, "primary");
        }

        var backupPath = Path.Combine(AegisMirrorPaths.Root, "recovery.manifest.backup.json");
        if (!File.Exists(backupPath))
        {
            return (primary, primaryValid, "primary");
        }

        try
        {
            var backup = JsonSerializer.Deserialize<AegisRecoveryManifest>(File.ReadAllText(backupPath), JsonOptions);
            if (backup is null)
            {
                return (primary, primaryValid, "primary");
            }

            if (!File.Exists(AegisMirrorPaths.ManifestSignatureFile))
            {
                return (backup, false, "backup");
            }

            var signingJson = AegisManifestSigner.SerializeForSigning(backup);
            var signature = File.ReadAllText(AegisMirrorPaths.ManifestSignatureFile).Trim();
            var valid = AegisManifestSigner.VerifyManifestJson(signingJson, signature);
            if (valid)
            {
                File.Copy(backupPath, AegisMirrorPaths.ManifestFile, overwrite: true);
                return (backup, true, "backup-quorum");
            }
        }
        catch
        {
            // Fall through.
        }

        return (primary, primaryValid, "primary");
    }
}