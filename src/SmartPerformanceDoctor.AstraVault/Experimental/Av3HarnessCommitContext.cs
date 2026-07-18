using System.Security.Cryptography;

namespace SmartPerformanceDoctor.AstraVault.Experimental;

/// <summary>Test-only VMK and object payload for harness AEAD (isolated temp; not user vaults).</summary>
public sealed class Av3HarnessCommitContext
{
    public const string ObjectKeyDomain = "astra-harness-object";

    public byte[] Vmk { get; init; } = [];
    public byte[] HarnessObjectPlaintext { get; init; } = [];

    public static Av3HarnessCommitContext Generate(Av3WritePlan plan)
    {
        var seed = plan.ContainerId.ToByteArray();
        var plain = SHA256.HashData([.. seed, .. BitConverter.GetBytes(plan.TargetGeneration)]);
        return new Av3HarnessCommitContext
        {
            Vmk = RandomNumberGenerator.GetBytes(32),
            HarnessObjectPlaintext = plain
        };
    }
}