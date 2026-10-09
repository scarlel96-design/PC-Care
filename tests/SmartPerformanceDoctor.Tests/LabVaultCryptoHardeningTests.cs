using System.Security.Cryptography;
using SmartPerformanceDoctor.SecurityLab.VaultV4;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class LabVaultCryptoHardeningTests
{
    [Fact]
    public void Concealed_v2_padding_round_trips_and_has_its_own_format_magic()
    {
        var key = LabVaultCrypto.GenerateKey();
        var plain = "small encrypted payload"u8.ToArray();
        var aad = "vault-object-aad"u8.ToArray();
        try
        {
            var blob = LabVaultCrypto.EncryptChunked(key, plain, aad, LabContentSuite.XChaCha20Poly1305, concealedPad: true);
            Assert.Equal("SPDCHK5"u8.ToArray(), blob.AsSpan(0, 7).ToArray());
            Assert.Equal(plain, LabVaultCrypto.DecryptChunked(key, blob, aad));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    [Fact]
    public void Unpadded_4096_byte_payload_is_never_mistaken_for_legacy_padding()
    {
        var key = LabVaultCrypto.GenerateKey();
        var plain = new byte[LabVaultCrypto.ConcealedPadBlock];
        RandomNumberGenerator.Fill(plain);
        plain[^3] = 0xA5;
        plain[^2] = 0x04;
        plain[^1] = 0x00;
        var aad = "vault-object-aad"u8.ToArray();
        try
        {
            var blob = LabVaultCrypto.EncryptChunked(key, plain, aad, LabContentSuite.XChaCha20Poly1305, concealedPad: false);
            Assert.Equal(plain, LabVaultCrypto.DecryptChunked(key, blob, aad));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_chunk_counts_fail_closed(int count)
    {
        var blob = new byte[11];
        "SPDCHK4"u8.CopyTo(blob);
        BitConverter.GetBytes(count).CopyTo(blob, 7);
        var key = LabVaultCrypto.GenerateKey();
        try
        {
            Assert.Throws<CryptographicException>(() => LabVaultCrypto.DecryptChunked(key, blob, "aad"u8.ToArray()));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    [Fact]
    public void Oversized_kdf_parameters_are_rejected_before_argon_allocation()
    {
        Assert.Throws<CryptographicException>(() =>
            LabVaultCrypto.DeriveArgon2id("ValidPassword99!", new byte[32], 3, LabParserGuard.MaxKdfMemoryKb + 1, 4));
    }
}