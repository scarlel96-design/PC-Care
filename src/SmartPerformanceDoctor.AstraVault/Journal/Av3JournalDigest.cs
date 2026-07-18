using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Journal;

public static class Av3JournalDigest
{
    public const int RecordDigestOffset = 200;
    public const int RecordDigestSize = 32;
    public const int HeaderBytesHashed = 200;

    public static byte[] ComputeRecordDigest(ReadOnlySpan<byte> descriptorWithoutEmbeddedDigest)
    {
        if (descriptorWithoutEmbeddedDigest.Length < HeaderBytesHashed)
        {
            throw new ArgumentException("Journal descriptor too short for digest.", nameof(descriptorWithoutEmbeddedDigest));
        }

        return SHA256.HashData(descriptorWithoutEmbeddedDigest.Slice(0, HeaderBytesHashed));
    }
}