using System.Security.Cryptography;

namespace SmartPerformanceDoctor.Aegis;

public static class AegisKeyProtector
{
    private const string TpmWrapKeyName = "AstraCare.AegisMirror.CapsuleDekWrap";

    public static (byte[] WrappedKey, string Mode) ProtectKey(byte[] dek)
    {
        if (TryProtectWithTpm(dek, out var tpmWrapped))
        {
            return (tpmWrapped, "tpm-pcp");
        }

        var dpapi = ProtectedData.Protect(dek, null, DataProtectionScope.LocalMachine);
        return (dpapi, "dpapi-localmachine");
    }

    public static byte[] UnprotectKey(byte[] wrappedKey, string mode)
    {
        if (string.Equals(mode, "tpm-pcp", StringComparison.OrdinalIgnoreCase))
        {
            return UnprotectWithTpm(wrappedKey);
        }

        return ProtectedData.Unprotect(wrappedKey, null, DataProtectionScope.LocalMachine);
    }

    public static bool IsTpmAvailable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            _ = new CngProvider("Microsoft Platform Crypto Provider");
            using var probe = CngKey.Open(TpmWrapKeyName, new CngProvider("Microsoft Platform Crypto Provider"), CngKeyOpenOptions.MachineKey);
            return probe is not null;
        }
        catch (CryptographicException)
        {
            try
            {
                var creation = new CngKeyCreationParameters
                {
                    Provider = new CngProvider("Microsoft Platform Crypto Provider"),
                    KeyCreationOptions = CngKeyCreationOptions.MachineKey | CngKeyCreationOptions.OverwriteExistingKey,
                    ExportPolicy = CngExportPolicies.None
                };
                using var created = CngKey.Create(CngAlgorithm.Rsa, TpmWrapKeyName, creation);
                return created is not null;
            }
            catch
            {
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool TryProtectWithTpm(byte[] dek, out byte[] wrapped)
    {
        wrapped = [];
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var key = OpenOrCreateTpmWrapKey();
            using var rsa = new RSACng(key);
            wrapped = rsa.Encrypt(dek, RSAEncryptionPadding.OaepSHA256);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] UnprotectWithTpm(byte[] wrapped)
    {
        using var key = CngKey.Open(TpmWrapKeyName, new CngProvider("Microsoft Platform Crypto Provider"), CngKeyOpenOptions.MachineKey);
        using var rsa = new RSACng(key);
        return rsa.Decrypt(wrapped, RSAEncryptionPadding.OaepSHA256);
    }

    private static CngKey OpenOrCreateTpmWrapKey()
    {
        try
        {
            return CngKey.Open(TpmWrapKeyName, new CngProvider("Microsoft Platform Crypto Provider"), CngKeyOpenOptions.MachineKey);
        }
        catch (CryptographicException)
        {
            var creation = new CngKeyCreationParameters
            {
                Provider = new CngProvider("Microsoft Platform Crypto Provider"),
                KeyCreationOptions = CngKeyCreationOptions.MachineKey | CngKeyCreationOptions.OverwriteExistingKey,
                ExportPolicy = CngExportPolicies.None
            };
            return CngKey.Create(CngAlgorithm.Rsa, TpmWrapKeyName, creation);
        }
    }
}