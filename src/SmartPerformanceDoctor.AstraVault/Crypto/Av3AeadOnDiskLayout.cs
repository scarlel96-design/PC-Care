namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>Suite-specific on-disk nonce/tag offsets (TARGET suite 3 uses extended nonce field).</summary>
public static class Av3AeadOnDiskLayout
{
    public static (int NonceOffset, int NonceLength, int TagOffset, int CiphertextLengthOffset) MetadataRootNonceTagOffsets(ushort cipherSuiteId)
    {
        if (cipherSuiteId == Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24)
        {
            return (104, AstraAead.XChaChaNonceSize, 128, 144);
        }

        var nonceLen = cipherSuiteId == Av3AeadAlgorithmId.Aes256Gcm
            ? AstraAead.AesNonceSize
            : AstraAead.ChaChaNonceSize;
        return (104, nonceLen, 116, 132);
    }

    public static (int NonceOffset, int NonceLength, int TagOffset) HeaderActivationOffsets(ushort cipherSuiteId)
    {
        if (cipherSuiteId == Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24)
        {
            return (192, AstraAead.XChaChaNonceSize, 216);
        }

        var nonceLen = cipherSuiteId == Av3AeadAlgorithmId.Aes256Gcm
            ? AstraAead.AesNonceSize
            : AstraAead.ChaChaNonceSize;
        return (192, nonceLen, 204);
    }
}