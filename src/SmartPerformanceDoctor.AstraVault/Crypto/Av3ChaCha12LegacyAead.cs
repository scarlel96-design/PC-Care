namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>Transitional ChaCha20-Poly1305 (12-byte nonce) — BELOW S-CLASS.</summary>
public sealed class Av3ChaCha12LegacyAead : IAv3AeadCipher
{
    public static Av3ChaCha12LegacyAead Instance { get; } = new();

    public ushort AlgorithmId => Av3AeadAlgorithmId.ChaCha12Transitional;

    public int NonceSize => AstraAead.ChaChaNonceSize;

    public AstraCiphertext Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData) =>
        AstraAead.Encrypt(AstraCipherSuite.XChaCha20Poly1305, key, plaintext, associatedData);

    public byte[] Decrypt(ReadOnlySpan<byte> key, AstraCiphertext blob, ReadOnlySpan<byte> associatedData) =>
        AstraAead.Decrypt(AstraCipherSuite.XChaCha20Poly1305, key, blob, associatedData);
}