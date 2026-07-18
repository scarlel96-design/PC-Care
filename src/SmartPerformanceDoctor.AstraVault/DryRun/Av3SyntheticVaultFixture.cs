using System.Security.Cryptography;
using System.Text;
using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;

namespace SmartPerformanceDoctor.AstraVault.DryRun;

public enum Av3SyntheticFixtureKind
{
    Standard = 0,
    MultiObject = 1,
    MultiSegment = 2,
    EmptyObject = 3,
    XChaCha24Synthetic = 4
}

/// <summary>Deterministic synthetic vault inputs for isolated dry-run (TEST ONLY).</summary>
public sealed class Av3SyntheticVaultFixture
{
    public const string TestOnlyLabel = "AV3_SYNTHETIC_TEST_ONLY";

    public Guid ContainerId { get; init; }

    public Guid VaultId { get; init; }

    public ulong PreviousGeneration { get; init; } = 3;

    public ulong TargetGeneration { get; init; } = 4;

    public Av3SyntheticFixtureKind Kind { get; init; } = Av3SyntheticFixtureKind.Standard;

    public Av3SyntheticObjectSet ObjectSet { get; init; } = new();

    public ushort HarnessCipherSuiteId =>
        Kind == Av3SyntheticFixtureKind.XChaCha24Synthetic
            ? Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24
            : Av3HarnessCommitCrypto.HarnessCipherSuite;

    public static Av3SyntheticVaultFixture Create(Av3SyntheticFixtureKind kind = Av3SyntheticFixtureKind.Standard)
    {
        var container = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var vault = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var (objects, segments) = kind switch
        {
            Av3SyntheticFixtureKind.MultiObject => (
                new[] { Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1"), Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc2") },
                new[] { Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd01") }),
            Av3SyntheticFixtureKind.MultiSegment => (
                new[] { Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc3") },
                new[]
                {
                    Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd02"),
                    Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd03")
                }),
            Av3SyntheticFixtureKind.EmptyObject => (Array.Empty<Guid>(), Array.Empty<Guid>()),
            _ => (
                new[] { Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc0") },
                new[] { Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd00") })
        };

        return new Av3SyntheticVaultFixture
        {
            ContainerId = container,
            VaultId = vault,
            Kind = kind,
            ObjectSet = new Av3SyntheticObjectSet
            {
                ObjectIds = objects,
                SegmentIds = segments
            }
        };
    }

    public Av3WritePlan BuildWritePlan()
    {
        var tx = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var objectDigest = DeriveDigest("object-write-set", (ulong)ObjectSet.ObjectCount, (ulong)ObjectSet.SegmentCount);
        return new Av3WritePlan
        {
            ContainerId = ContainerId,
            TransactionId = tx,
            PreviousGeneration = PreviousGeneration,
            TargetGeneration = TargetGeneration,
            PreviousMetadataRootDigest = Av3JournalDeterministicFixtures.DigestSlot0,
            Objects = new Av3ObjectWriteSet
            {
                ObjectWriteSetDigest = objectDigest,
                ObjectCount = ObjectSet.ObjectCount
            },
            Metadata = new Av3MetadataWriteSet
            {
                MetadataWriteDigest = Av3JournalDeterministicFixtures.DigestSlot3,
                TargetMetadataRootCiphertextDigest = DeriveDigest("metadata-root", TargetGeneration, PreviousGeneration)
            }
        };
    }

    public Av3HarnessCommitContext BuildDeterministicCryptoContext()
    {
        var plan = BuildWritePlan();
        var vmk = DeriveTestOnlyKey(plan.ContainerId, "vmk");
        var payloadSeed = DeriveDigest("object-payload", plan.TargetGeneration, (ulong)ObjectSet.ObjectCount);
        return new Av3HarnessCommitContext
        {
            Vmk = vmk,
            HarnessObjectPlaintext = payloadSeed
        };
    }

    internal static byte[] DeriveTestOnlyKey(Guid id, string domain)
    {
        var bytes = Encoding.UTF8.GetBytes($"{TestOnlyLabel}:{id:N}:{domain}");
        return SHA256.HashData(bytes);
    }

    private static byte[] DeriveDigest(string label, ulong a, ulong b)
    {
        var bytes = Encoding.UTF8.GetBytes($"{TestOnlyLabel}:{label}:{a}:{b}");
        return SHA256.HashData(bytes);
    }
}