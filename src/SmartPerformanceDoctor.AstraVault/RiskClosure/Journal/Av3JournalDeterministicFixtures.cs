using SmartPerformanceDoctor.AstraVault.Journal;

namespace SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;

/// <summary>Deterministic journal digests for R11 tests (no RNG false-positives).</summary>
public static class Av3JournalDeterministicFixtures
{
    public static readonly byte[] DigestSlot0 = CreatePattern(0x01);

    public static readonly byte[] DigestSlot1 = CreatePattern(0x41);

    public static readonly byte[] DigestSlot2 = CreatePattern(0x81);

    public static readonly byte[] DigestSlot3 = CreatePattern(0xC1);

    /// <summary>ASCII bytes V,M,K as digest prefix — must not fail binary structural scan (E-6.1).</summary>
    public static readonly byte[] DigestVmKAsciiTrap = CreateVmKTrap();

    public static Av3JournalDescriptor BuildDescriptor(
        ushort cipherSuiteId,
        Guid containerId,
        Guid? transactionId = null,
        ulong previousGeneration = 3,
        ulong targetGeneration = 4)
    {
        return new Av3JournalDescriptor
        {
            CipherSuiteId = cipherSuiteId,
            ContainerId = containerId,
            TransactionId = transactionId ?? Guid.Parse("22222222-2222-2222-2222-222222222222"),
            PreviousGeneration = previousGeneration,
            TargetGeneration = targetGeneration,
            PreviousMetadataRootCiphertextDigest = DigestSlot0,
            TargetMetadataRootCiphertextDigest = DigestSlot1,
            ObjectWriteSetDigest = DigestSlot2,
            MetadataWriteDigest = DigestSlot3,
            ActivationTarget = Av3JournalDescriptor.ActivationTargetMetadataRoot,
            State = Av3JournalState.JournalDurable,
            MonotonicTimestampUtc = 1
        };
    }

    private static byte[] CreatePattern(byte seed)
    {
        var digest = new byte[32];
        for (var i = 0; i < digest.Length; i++)
        {
            digest[i] = (byte)(seed + i);
        }

        return digest;
    }

    private static byte[] CreateVmKTrap()
    {
        var digest = new byte[32];
        digest[0] = 0x56;
        digest[1] = 0x4D;
        digest[2] = 0x4B;
        for (var i = 3; i < digest.Length; i++)
        {
            digest[i] = (byte)(0xA0 + i);
        }

        return digest;
    }
}