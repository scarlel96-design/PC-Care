using System.Security.Cryptography;
using System.Text;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// Design § crypto AAD / stream edge matrix (S-class).
/// Pure crypto cases — wrong generation, wrong vault id, empty stream, truncated cipher.
/// </summary>
public static class LabAadBoundary
{
    public sealed class CaseResult
    {
        public required string Name { get; init; }
        public required bool ExpectedPass { get; init; }
        public required bool ActualPass { get; init; }
        public string Message { get; init; } = "";
        public bool Pass => ExpectedPass == ActualPass;
    }

    public sealed class Report
    {
        public required IReadOnlyList<CaseResult> Cases { get; init; }
        public int Passed => Cases.Count(c => c.Pass);
        public int Total => Cases.Count;
        public bool AllPass => Cases.All(c => c.Pass);

        public string ToHumanSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"LabAadBoundary {Passed}/{Total} pass");
            foreach (var c in Cases)
            {
                sb.AppendLine($"  {(c.Pass ? "OK" : "FAIL")} {c.Name} expectedPass={c.ExpectedPass} actual={c.ActualPass} · {c.Message}");
            }

            return sb.ToString();
        }
    }

    public static Report Run()
    {
        var cases = new List<CaseResult>
        {
            CaseRoundtripGenBound(),
            CaseWrongGenerationFails(),
            CaseWrongVaultIdFails(),
            CaseLegacyAadStillDecryptsWhenUsedAtEncrypt(),
            CaseEmptyStreamRoundtrip(),
            CaseTruncatedCipherFails(),
            CaseStreamWrongAadFails(),
            CaseZeroLengthBytesRoundtrip()
        };
        return new Report { Cases = cases };
    }

    private static CaseResult CaseRoundtripGenBound()
    {
        try
        {
            var key = LabVaultCrypto.GenerateKey();
            var plain = Encoding.UTF8.GetBytes("aad-bound-payload");
            var aad = LabCryptoBroker.BuildObjectAad("vault-a", "entry-1", 3);
            var blob = LabVaultCrypto.EncryptChunked(key, plain, aad, LabContentSuite.XChaCha20Poly1305);
            var outP = LabVaultCrypto.DecryptChunked(key, blob, aad);
            var ok = plain.AsSpan().SequenceEqual(outP);
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plain);
            CryptographicOperations.ZeroMemory(outP);
            return R("roundtrip-gen-bound", true, ok, ok ? "ok" : "mismatch");
        }
        catch (Exception ex)
        {
            return R("roundtrip-gen-bound", true, false, ex.Message);
        }
    }

    private static CaseResult CaseWrongGenerationFails()
    {
        try
        {
            var key = LabVaultCrypto.GenerateKey();
            var plain = Encoding.UTF8.GetBytes("gen-mismatch");
            var aadEnc = LabCryptoBroker.BuildObjectAad("v1", "e1", 5);
            var aadDec = LabCryptoBroker.BuildObjectAad("v1", "e1", 6);
            var blob = LabVaultCrypto.EncryptChunked(key, plain, aadEnc, LabContentSuite.XChaCha20Poly1305);
            var failed = false;
            try
            {
                LabVaultCrypto.DecryptChunked(key, blob, aadDec);
            }
            catch (CryptographicException)
            {
                failed = true;
            }

            CryptographicOperations.ZeroMemory(key);
            return R("wrong-generation-fails", true, failed, failed ? "auth fail as expected" : "unexpected success");
        }
        catch (Exception ex)
        {
            return R("wrong-generation-fails", true, false, ex.Message);
        }
    }

    private static CaseResult CaseWrongVaultIdFails()
    {
        try
        {
            var key = LabVaultCrypto.GenerateKey();
            var plain = Encoding.UTF8.GetBytes("vault-id-bound");
            var aadEnc = LabCryptoBroker.BuildObjectAad("vault-A", "e1", 1);
            var aadDec = LabCryptoBroker.BuildObjectAad("vault-B", "e1", 1);
            var blob = LabVaultCrypto.EncryptChunked(key, plain, aadEnc, LabContentSuite.XChaCha20Poly1305);
            var failed = false;
            try
            {
                LabVaultCrypto.DecryptChunked(key, blob, aadDec);
            }
            catch (CryptographicException)
            {
                failed = true;
            }

            CryptographicOperations.ZeroMemory(key);
            return R("wrong-vaultid-fails", true, failed, failed ? "auth fail as expected" : "unexpected success");
        }
        catch (Exception ex)
        {
            return R("wrong-vaultid-fails", true, false, ex.Message);
        }
    }

    private static CaseResult CaseLegacyAadStillDecryptsWhenUsedAtEncrypt()
    {
        try
        {
            var key = LabVaultCrypto.GenerateKey();
            var plain = Encoding.UTF8.GetBytes("legacy-aad");
            var legacy = Encoding.UTF8.GetBytes("lab-obj:vid:eid");
            var blob = LabVaultCrypto.EncryptChunked(key, plain, legacy, LabContentSuite.XChaCha20Poly1305);
            var outP = LabVaultCrypto.DecryptChunked(key, blob, legacy);
            var ok = plain.AsSpan().SequenceEqual(outP);
            CryptographicOperations.ZeroMemory(key);
            return R("legacy-aad-encrypt-decrypt", true, ok, ok ? "legacy path ok" : "mismatch");
        }
        catch (Exception ex)
        {
            return R("legacy-aad-encrypt-decrypt", true, false, ex.Message);
        }
    }

    private static CaseResult CaseEmptyStreamRoundtrip()
    {
        try
        {
            var key = LabVaultCrypto.GenerateKey();
            var aad = LabCryptoBroker.BuildObjectAad("v", "e", 1);
            using var plainIn = new MemoryStream(Array.Empty<byte>());
            using var cipher = new MemoryStream();
            LabVaultCrypto.EncryptChunkedToFile(key, plainIn, cipher, aad, LabContentSuite.XChaCha20Poly1305);
            cipher.Position = 0;
            using var plainOut = new MemoryStream();
            LabVaultCrypto.DecryptChunkedFromFile(key, cipher, plainOut, aad);
            var ok = plainOut.Length == 0;
            CryptographicOperations.ZeroMemory(key);
            return R("empty-stream-roundtrip", true, ok, $"len={plainOut.Length}");
        }
        catch (Exception ex)
        {
            return R("empty-stream-roundtrip", true, false, ex.Message);
        }
    }

    private static CaseResult CaseZeroLengthBytesRoundtrip()
    {
        try
        {
            var key = LabVaultCrypto.GenerateKey();
            var aad = LabCryptoBroker.BuildObjectAad("v", "e", 2);
            var blob = LabVaultCrypto.EncryptChunked(key, Array.Empty<byte>(), aad, LabContentSuite.XChaCha20Poly1305);
            var outP = LabVaultCrypto.DecryptChunked(key, blob, aad);
            var ok = outP.Length == 0;
            CryptographicOperations.ZeroMemory(key);
            return R("zero-length-bytes-roundtrip", true, ok, $"len={outP.Length}");
        }
        catch (Exception ex)
        {
            return R("zero-length-bytes-roundtrip", true, false, ex.Message);
        }
    }

    private static CaseResult CaseTruncatedCipherFails()
    {
        try
        {
            var key = LabVaultCrypto.GenerateKey();
            var plain = Encoding.UTF8.GetBytes("truncate-me-please");
            var aad = LabCryptoBroker.BuildObjectAad("v", "e", 1);
            var blob = LabVaultCrypto.EncryptChunked(key, plain, aad, LabContentSuite.XChaCha20Poly1305);
            var cut = blob.AsSpan(0, Math.Max(8, blob.Length / 2)).ToArray();
            var failed = false;
            try
            {
                LabVaultCrypto.DecryptChunked(key, cut, aad);
            }
            catch (Exception)
            {
                failed = true;
            }

            CryptographicOperations.ZeroMemory(key);
            return R("truncated-cipher-fails", true, failed, failed ? "fail-closed" : "unexpected success");
        }
        catch (Exception ex)
        {
            return R("truncated-cipher-fails", true, false, ex.Message);
        }
    }

    private static CaseResult CaseStreamWrongAadFails()
    {
        try
        {
            var key = LabVaultCrypto.GenerateKey();
            var aadOk = LabCryptoBroker.BuildObjectAad("v", "e", 1);
            var aadBad = LabCryptoBroker.BuildObjectAad("v", "e", 99);
            var plain = Encoding.UTF8.GetBytes(new string('S', 4000));
            using var plainIn = new MemoryStream(plain);
            using var cipher = new MemoryStream();
            LabVaultCrypto.EncryptChunkedToFile(key, plainIn, cipher, aadOk, LabContentSuite.XChaCha20Poly1305);
            cipher.Position = 0;
            using var plainOut = new MemoryStream();
            var failed = false;
            try
            {
                LabVaultCrypto.DecryptChunkedFromFile(key, cipher, plainOut, aadBad);
            }
            catch (Exception)
            {
                failed = true;
            }

            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plain);
            return R("stream-wrong-aad-fails", true, failed, failed ? "stream auth fail" : "unexpected success");
        }
        catch (Exception ex)
        {
            return R("stream-wrong-aad-fails", true, false, ex.Message);
        }
    }

    private static CaseResult R(string name, bool expected, bool actual, string msg) =>
        new() { Name = name, ExpectedPass = expected, ActualPass = actual, Message = msg };
}
