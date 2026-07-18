namespace SmartPerformanceDoctor.AstraVault.DryRun;

/// <summary>Synthetic object/segment ids for E-8 dry-run (TEST ONLY — no user filenames).</summary>
public sealed class Av3SyntheticObjectSet
{
    public const string TestOnlyMarker = "AV3_SYNTHETIC_TEST_ONLY";

    public IReadOnlyList<Guid> ObjectIds { get; init; } = [];

    public IReadOnlyList<Guid> SegmentIds { get; init; } = [];

    public int ObjectCount => ObjectIds.Count;

    public int SegmentCount => SegmentIds.Count;
}