using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SmartPerformanceDoctor.Aegis;

internal static class AegisManifestSigner
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static bool VerifyManifestJson(string manifestJson, string signatureBase64)
    {
        if (string.IsNullOrWhiteSpace(signatureBase64))
        {
            return false;
        }

        try
        {
            var signature = Convert.FromBase64String(signatureBase64);
            using var verifier = AegisSigningKeys.CreateVerifier();
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(manifestJson));
            return verifier.VerifyHash(hash, signature);
        }
        catch
        {
            return false;
        }
    }

    public static string SerializeForSigning(AegisRecoveryManifest manifest)
    {
        var clone = new AegisRecoveryManifest
        {
            Product = manifest.Product,
            Version = manifest.Version,
            CreatedAt = manifest.CreatedAt,
            Files = manifest.Files,
            CapsuleHash = manifest.CapsuleHash,
            CapsuleVersion = manifest.CapsuleVersion
        };
        return JsonSerializer.Serialize(clone, CanonicalOptions);
    }

    internal static bool TryWriteSignature(string manifestJson, string signatureFilePath)
    {
        if (!AegisManifestBuildSigner.TrySignManifestJson(manifestJson, out var signature))
        {
            return false;
        }

        File.WriteAllText(signatureFilePath, signature);
        return true;
    }
}