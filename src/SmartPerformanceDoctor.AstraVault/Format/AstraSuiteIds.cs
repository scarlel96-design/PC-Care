namespace SmartPerformanceDoctor.AstraVault.Format;

public static class AstraSuiteIds
{
    public const ushort KdfArgon2id = 1;

    public static bool IsSupportedKdf(ushort id) => id is KdfArgon2id;

    public static bool IsSupportedCipher(ushort id) =>
        id is (ushort)Crypto.AstraCipherSuite.XChaCha20Poly1305
            or (ushort)Crypto.AstraCipherSuite.Aes256Gcm
            or (ushort)Crypto.AstraCipherSuite.XChaCha20Poly1305Ietf24;
}