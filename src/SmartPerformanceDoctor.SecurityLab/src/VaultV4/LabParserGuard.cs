using System.Security.Cryptography;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>Fail-closed size/version bounds (design §13 subset).</summary>
public static class LabParserGuard
{
    public const int MaxHeaderBytes = 64 * 1024;
    public const int MaxMetadataBytes = 32 * 1024 * 1024;
    public const long MaxObjectBytes = 512L * 1024 * 1024;
    public const int MaxEntries = 50_000;
    public const int MaxObjectIdHexLen = 64;

    public static void EnsureHeaderSize(int length)
    {
        if (length <= 0 || length > MaxHeaderBytes)
        {
            throw new CryptographicException("header size out of bounds");
        }
    }

    public static void EnsureMetadataSize(int length)
    {
        if (length <= 0 || length > MaxMetadataBytes)
        {
            throw new CryptographicException("metadata size out of bounds");
        }
    }

    public static void EnsureObjectSize(long length)
    {
        if (length < 0 || length > MaxObjectBytes)
        {
            throw new CryptographicException("object size out of bounds");
        }
    }

    public static void EnsureObjectId(string objectId)
    {
        if (string.IsNullOrWhiteSpace(objectId)
            || objectId.Length < 4
            || objectId.Length > MaxObjectIdHexLen
            || !objectId.All(static c =>
                c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F')))
        {
            throw new CryptographicException("invalid object id");
        }
    }

    public static void EnsureEntryCount(int count)
    {
        if (count < 0 || count > MaxEntries)
        {
            throw new CryptographicException("entry count out of bounds");
        }
    }
}
