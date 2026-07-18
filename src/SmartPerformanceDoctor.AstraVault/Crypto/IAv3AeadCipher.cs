namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>AEAD primitive contract (algorithm id authenticated via AAD builders).</summary>
public interface IAv3AeadCipher
{
    ushort AlgorithmId { get; }

    int NonceSize { get; }

    AstraCiphertext Encrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData);

    byte[] Decrypt(ReadOnlySpan<byte> key, AstraCiphertext blob, ReadOnlySpan<byte> associatedData);
}