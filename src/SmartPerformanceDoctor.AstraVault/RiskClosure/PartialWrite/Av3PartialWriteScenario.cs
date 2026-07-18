namespace SmartPerformanceDoctor.AstraVault.RiskClosure.PartialWrite;

public enum Av3PartialWriteMode
{
    Truncation = 1,
    TrailingGarbageAppend = 2,
    ZeroFilledTail = 3,
    RandomByteCorruption = 4,
    SectorBoundarySplit = 5
}

/// <summary>Deterministic torn-write scenario (isolated harness only).</summary>
public sealed class Av3PartialWriteScenario
{
    public Av3WriteBoundary Boundary { get; init; }
    public Av3PartialWriteMode Mode { get; init; } = Av3PartialWriteMode.Truncation;

    /// <summary>Bytes to keep for truncation / split; corruption offset for random corruption.</summary>
    public int Parameter { get; init; } = 16;

    public int SectorSize { get; init; } = 512;
}