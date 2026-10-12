using System.Security.Cryptography;
using SmartPerformanceDoctor.SecurityLab.VaultV4;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WO14StreamingCompatibilityTests
{
    [Theory]
    [InlineData(LabContentSuite.Aes256Gcm, 0)]
    [InlineData(LabContentSuite.Aes256Gcm, 4096)]
    [InlineData(LabContentSuite.Aes256Gcm, 2097185)]
    [InlineData(LabContentSuite.XChaCha20Poly1305, 0)]
    [InlineData(LabContentSuite.XChaCha20Poly1305, 4096)]
    [InlineData(LabContentSuite.XChaCha20Poly1305, 2097185)]
    public void Streaming_reader_matches_existing_container_bytes(LabContentSuite suite, int size)
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var plain = RandomNumberGenerator.GetBytes(size);
        byte[] aad = [2, 3, 4];
        using var input = new MemoryStream(LabVaultCrypto.EncryptChunked(key, plain, aad, suite));
        using var output = new MemoryStream();
        LabVaultCrypto.DecryptChunkedFromFile(key, input, output, aad);
        Assert.Equal(plain, output.ToArray());
    }

    [Fact] public void Concealed_padding_preserves_content_length()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        byte[] plain = [1, 2, 3];
        using var input = new MemoryStream(LabVaultCrypto.EncryptChunked(key, plain, [], concealedPad: true));
        using var output = new MemoryStream();
        LabVaultCrypto.DecryptChunkedFromFile(key, input, output, []);
        Assert.Equal(plain, output.ToArray());
    }

    [Theory]
    [InlineData("tag")]
    [InlineData("truncated")]
    [InlineData("trailing")]
    public void Invalid_container_is_not_reported_as_success(string damage)
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var blob = LabVaultCrypto.EncryptChunked(key, [1, 2, 3], []);
        if (damage == "tag") blob[11 + LabVaultCrypto.XChaChaNonceSize] ^= 1;
        if (damage == "truncated") blob = blob[..^1];
        if (damage == "trailing") blob = [..blob, 1];
        using var input = new MemoryStream(blob);
        using var output = new MemoryStream();
        Assert.ThrowsAny<CryptographicException>(() => LabVaultCrypto.DecryptChunkedFromFile(key, input, output, []));
        if (damage != "trailing") Assert.Equal(0, output.Length);
    }

    [Fact] public void Cancellation_stops_before_publishing_plaintext()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        using var input = new MemoryStream(LabVaultCrypto.EncryptChunked(key, [1, 2, 3], []));
        using var output = new MemoryStream();
        using var stop = new CancellationTokenSource(); stop.Cancel();
        Assert.Throws<OperationCanceledException>(() => LabVaultCrypto.DecryptChunkedFromFile(key, input, output, [], stop.Token));
        Assert.Equal(0, output.Length);
    }
}
