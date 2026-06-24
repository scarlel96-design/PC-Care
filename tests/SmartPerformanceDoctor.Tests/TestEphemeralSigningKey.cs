using System.Security.Cryptography;
using System.Text;

namespace SmartPerformanceDoctor.Tests;

internal static class TestEphemeralSigningKey
{
    public static string CreatePemFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"aegis-test-{Guid.NewGuid():N}.pem");
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = ecdsa.ExportPkcs8PrivateKeyPem();
        File.WriteAllText(path, pem, Encoding.UTF8);
        return path;
    }
}