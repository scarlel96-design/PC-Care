using System.Security.Cryptography;
using System.Text;
using SmartPerformanceDoctor.SecurityLab.VaultV4;

// Minimal smoke (run via `dotnet test` once test project is added, or:
//   dotnet run --project ... if converted to executable harness)
// For now compile-only verification with the library build.

namespace SmartPerformanceDoctor.SecurityLab.Tests;

public static class LabCryptoSmoke
{
    public static void Run()
    {
        var key = LabVaultCrypto.GenerateKey();
        var plain = Encoding.UTF8.GetBytes("lab-chunk-" + new string('Z', 3000));
        var aad = "lab-aad"u8.ToArray();
        var blob = LabVaultCrypto.EncryptChunked(key, plain, aad);
        var back = LabVaultCrypto.DecryptChunked(key, blob, aad);
        if (!plain.AsSpan().SequenceEqual(back))
        {
            throw new InvalidOperationException("chunk round-trip failed");
        }

        CryptographicOperations.ZeroMemory(key);
        CryptographicOperations.ZeroMemory(plain);
        CryptographicOperations.ZeroMemory(back);
    }
}
