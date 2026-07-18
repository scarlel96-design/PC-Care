namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>S-Class TARGET XChaCha20-Poly1305 (24-byte nonce).</summary>
public sealed class Av3XChaCha24Aead : IAv3AeadCipher
{
    public static Av3XChaCha24Aead Instance { get; } = new();

    public ushort AlgorithmId => Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24;

    public int NonceSize => AstraAead.XChaChaNonceSize;

    public AstraCiphertext Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData) =>
        AstraAead.Encrypt(AstraCipherSuite.XChaCha20Poly1305Ietf24, key, plaintext, associatedData);

    public byte[] Decrypt(ReadOnlySpan<byte> key, AstraCiphertext blob, ReadOnlySpan<byte> associatedData) =>
        AstraAead.Decrypt(AstraCipherSuite.XChaCha20Poly1305Ietf24, key, blob, associatedData);
}