using System.Buffers.Binary;
using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Experimental;

namespace SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;

/// <summary>Test fixture plan file (isolated temp only; no user paths/passwords).</summary>
public static class Av3KillPlanFixture
{
    public const int Magic = 0x4B503341; // APKP

    public static string WriteToTempFile(Av3WritePlan plan, byte[] vmk, byte[] objectPlaintext)
    {
        var path = Path.Combine(Path.GetTempPath(), "av3-e3-plan-" + Guid.NewGuid().ToString("N") + ".bin");
        var buf = new byte[4 + 16 + 16 + 8 + 8 + 32 + 32 + objectPlaintext.Length];
        var span = buf.AsSpan();
        BinaryPrimitives.WriteInt32LittleEndian(span, Magic);
        plan.ContainerId.TryWriteBytes(span.Slice(4, 16));
        plan.TransactionId.TryWriteBytes(span.Slice(20, 16));
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(36), plan.PreviousGeneration);
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(44), plan.TargetGeneration);
        vmk.CopyTo(span.Slice(52, 32));
        plan.PreviousMetadataRootDigest.CopyTo(span.Slice(84, 32));
        var tail = new byte[objectPlaintext.Length];
        objectPlaintext.CopyTo(tail, 0);
        File.WriteAllBytes(path, [.. buf, .. tail]);
        return path;
    }

    public static (Av3WritePlan Plan, Av3HarnessCommitContext Context) Load(string path)
    {
        var data = File.ReadAllBytes(path);
        if (data.Length < 116 || BinaryPrimitives.ReadInt32LittleEndian(data) != Magic)
        {
            throw new InvalidDataException("Invalid kill plan fixture.");
        }

        var objectPlain = data.AsSpan(116).ToArray();
        var plan = new Av3WritePlan
        {
            ContainerId = new Guid(data.AsSpan(4, 16)),
            TransactionId = new Guid(data.AsSpan(20, 16)),
            PreviousGeneration = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(36)),
            TargetGeneration = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(44)),
            PreviousMetadataRootDigest = data.AsSpan(84, 32).ToArray(),
            Objects = new Av3ObjectWriteSet { ObjectWriteSetDigest = RandomNumberGenerator.GetBytes(32) },
            Metadata = new Av3MetadataWriteSet
            {
                MetadataWriteDigest = RandomNumberGenerator.GetBytes(32),
                TargetMetadataRootCiphertextDigest = RandomNumberGenerator.GetBytes(32)
            }
        };
        var ctx = new Av3HarnessCommitContext
        {
            Vmk = data.AsSpan(52, 32).ToArray(),
            HarnessObjectPlaintext = objectPlain
        };
        return (plan, ctx);
    }
}