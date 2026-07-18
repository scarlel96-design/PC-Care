namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Harness anchor update prepared after post-auth commit.</summary>
public sealed class Av3AnchorUpdateRequest
{
    public string VaultRoot { get; init; } = string.Empty;

    public bool TestHarnessInvocation { get; init; }

    public Guid ContainerId { get; init; }

    public ulong TargetGeneration { get; init; }

    public ReadOnlyMemory<byte> WitnessDigest { get; init; }

    public Guid UpdateId { get; init; }
}