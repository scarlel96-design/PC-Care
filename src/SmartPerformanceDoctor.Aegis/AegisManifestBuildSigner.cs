using System.Security.Cryptography;
using System.Text;

namespace SmartPerformanceDoctor.Aegis;

/// <summary>
/// Build/CI/test-only manifest signing. Requires AEGIS_SIGNING_KEY_PATH pointing to a PEM private key file.
/// Production runtime must not set this variable.
/// </summary>
internal static class AegisManifestBuildSigner
{
    public const string SigningKeyPathVariable = "AEGIS_SIGNING_KEY_PATH";

    public static bool CanSign => TryResolvePrivateKeyPem(out _);

    public static bool IsSigningConfigured() => CanSign;

    public static bool TrySignManifestJson(string manifestJson, out string signatureBase64)
    {
        signatureBase64 = string.Empty;
        if (!TryResolvePrivateKeyPem(out var privateKeyPem))
        {
            return false;
        }

        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(privateKeyPem);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(manifestJson));
            signatureBase64 = Convert.ToBase64String(ecdsa.SignHash(hash));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolvePrivateKeyPem(out string pem)
    {
        pem = string.Empty;
        foreach (var path in EnumerateSigningKeyPaths())
        {
            if (!TryReadPrivateKeyPem(path, out pem))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    internal static IEnumerable<string> EnumerateSigningKeyPaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in EnumerateSigningKeyPathCandidates())
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            string resolved;
            try
            {
                resolved = Path.GetFullPath(path);
            }
            catch
            {
                continue;
            }

            if (seen.Add(resolved))
            {
                yield return resolved;
            }
        }
    }

    private static IEnumerable<string> EnumerateSigningKeyPathCandidates()
    {
        var envPath = Environment.GetEnvironmentVariable(SigningKeyPathVariable);
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            yield return envPath;
        }

        var installRoot = AegisRuntimeContext.InstallRoot;
        yield return Path.Combine(installRoot, "artifacts", "signing", "aegis-dev-private.pem");
        yield return Path.Combine(installRoot, "signing", "aegis-dev-private.pem");
        yield return Path.Combine(installRoot, "engine", "signing", "aegis-dev-private.pem");

        var baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!baseDir.Equals(installRoot, StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(baseDir, "artifacts", "signing", "aegis-dev-private.pem");
            yield return Path.Combine(baseDir, "signing", "aegis-dev-private.pem");
        }
    }

    private static bool TryReadPrivateKeyPem(string path, out string pem)
    {
        pem = string.Empty;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            pem = File.ReadAllText(path);
            return pem.Contains("PRIVATE KEY", StringComparison.Ordinal) && pem.Contains("BEGIN", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}