using System.Buffers.Binary;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>HChaCha20 subkey derivation for XChaCha20-Poly1305 (libsodium-compatible layout).</summary>
internal static class HChaCha20
{
    private static ReadOnlySpan<byte> Sigma => "expand 32-byte k"u8;

    public static byte[] DeriveSubkey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce16)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("Key must be 32 bytes.", nameof(key));
        }

        if (nonce16.Length != 16)
        {
            throw new ArgumentException("HChaCha nonce must be 16 bytes.", nameof(nonce16));
        }

        Span<uint> state = stackalloc uint[16];
        LoadState(state, key, nonce16);
        ChaChaCore(20, state);
        var subkey = new byte[32];
        BinaryPrimitives.WriteUInt32LittleEndian(subkey.AsSpan(0), state[0]);
        BinaryPrimitives.WriteUInt32LittleEndian(subkey.AsSpan(4), state[1]);
        BinaryPrimitives.WriteUInt32LittleEndian(subkey.AsSpan(8), state[2]);
        BinaryPrimitives.WriteUInt32LittleEndian(subkey.AsSpan(12), state[3]);
        BinaryPrimitives.WriteUInt32LittleEndian(subkey.AsSpan(16), state[12]);
        BinaryPrimitives.WriteUInt32LittleEndian(subkey.AsSpan(20), state[13]);
        BinaryPrimitives.WriteUInt32LittleEndian(subkey.AsSpan(24), state[14]);
        BinaryPrimitives.WriteUInt32LittleEndian(subkey.AsSpan(28), state[15]);
        return subkey;
    }

    private static void LoadState(Span<uint> state, ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce16)
    {
        state[0] = BinaryPrimitives.ReadUInt32LittleEndian(Sigma);
        state[1] = BinaryPrimitives.ReadUInt32LittleEndian(Sigma[4..]);
        state[2] = BinaryPrimitives.ReadUInt32LittleEndian(Sigma[8..]);
        state[3] = BinaryPrimitives.ReadUInt32LittleEndian(Sigma[12..]);
        for (var i = 0; i < 8; i++)
        {
            state[4 + i] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(i * 4, 4));
        }

        for (var i = 0; i < 4; i++)
        {
            state[12 + i] = BinaryPrimitives.ReadUInt32LittleEndian(nonce16.Slice(i * 4, 4));
        }
    }

    private static void ChaChaCore(int rounds, Span<uint> state)
    {
        Span<uint> working = stackalloc uint[16];
        state.CopyTo(working);
        for (var i = 0; i < rounds; i += 2)
        {
            QuarterRound(working, 0, 4, 8, 12);
            QuarterRound(working, 1, 5, 9, 13);
            QuarterRound(working, 2, 6, 10, 14);
            QuarterRound(working, 3, 7, 11, 15);
            QuarterRound(working, 0, 5, 10, 15);
            QuarterRound(working, 1, 6, 11, 12);
            QuarterRound(working, 2, 7, 8, 13);
            QuarterRound(working, 3, 4, 9, 14);
        }

        for (var i = 0; i < 16; i++)
        {
            state[i] += working[i];
        }
    }

    private static void QuarterRound(Span<uint> x, int a, int b, int c, int d)
    {
        x[a] += x[b];
        x[d] = RotateLeft(x[d] ^ x[a], 16);
        x[c] += x[d];
        x[b] = RotateLeft(x[b] ^ x[c], 12);
        x[a] += x[b];
        x[d] = RotateLeft(x[d] ^ x[a], 8);
        x[c] += x[d];
        x[b] = RotateLeft(x[b] ^ x[c], 7);
    }

    private static uint RotateLeft(uint v, int bits) => (v << bits) | (v >> (32 - bits));
}