using System.Security.Cryptography;

namespace SmartPerformanceDoctor.Aegis;

/// <summary>
/// Runtime trust anchor — public key only. Private key must never ship in product binaries.
/// Build/CI signing uses <see cref="AegisManifestBuildSigner"/>.
/// </summary>
internal static class AegisSigningKeys
{
    private const string PublicKeyPem =
        """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEH0srZcMD6tbrRQE6hW7L9LDEQaim
        jLiwPZqFgaWIg57Vq9aYMeXvarn0P43JtkwF+W2zs07ab64oOwJPqOYaTw==
        -----END PUBLIC KEY-----
        """;

    public static ECDsa CreateVerifier()
    {
        if (TryCreateVerifierFromSigningKeyPath(out var verifier))
        {
            return verifier;
        }

        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(PublicKeyPem);
        return ecdsa;
    }

    /// <summary>
    /// When AEGIS_SIGNING_KEY_PATH is set (build/CI/test), verify against that key's public half
    /// so ephemeral test keys work without shipping private material in the product.
    /// </summary>
    private static bool TryCreateVerifierFromSigningKeyPath(out ECDsa verifier)
    {
        verifier = null!;
        foreach (var path in AegisManifestBuildSigner.EnumerateSigningKeyPaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                var pem = File.ReadAllText(path);
                if (!pem.Contains("PRIVATE KEY", StringComparison.Ordinal))
                {
                    continue;
                }

                using var temp = ECDsa.Create();
                temp.ImportFromPem(pem);
                verifier = ECDsa.Create();
                verifier.ImportParameters(temp.ExportParameters(false));
                return true;
            }
            catch
            {
                // Try next candidate path.
            }
        }

        return false;
    }
}