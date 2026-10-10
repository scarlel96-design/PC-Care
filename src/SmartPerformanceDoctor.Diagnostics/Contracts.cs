namespace SmartPerformanceDoctor.Diagnostics;

public sealed record SamplingPolicy(int DurationSeconds = 30, int IntervalMilliseconds = 1000, int MaxProcesses = 128, int EventQueueCapacity = 4096)
{
    public string EtwProfile { get; init; } = "General";
    public void Validate()
    {
        if (DurationSeconds is < 2 or > 120 || IntervalMilliseconds is < 1000 or > 5000 || MaxProcesses is < 1 or > 128
            || EventQueueCapacity is < 32 or > 4096 || EtwProfile is not ("General" or "LowOverhead" or "Detailed" or "Disabled")) throw new ArgumentOutOfRangeException(nameof(SamplingPolicy));
    }
}
public sealed record ProcessMetric(string Identifier, double? CpuPercent, long WorkingSetBytes, double CpuTimeSeconds);
public sealed record DiskMetric(int DiskNumber, long ReadBytes, long WriteBytes, int Operations, double? P95LatencyMilliseconds)
{ public int LatencySampleCount { get; init; } public string LatencySampling { get; init; } = "Reservoir256"; }
public sealed record SpaceMetric(string Volume, double FreePercent);
public sealed record PerformanceSample(double Seconds, double? CpuPercent, double? AvailableMemoryPercent, double? CommitPercent,
    double? DiskLatencyMilliseconds, double? DiskQueue, double? ReadBytesPerSecond, double? WriteBytesPerSecond,
    double? IoPerSecond, double? PageReadsPerSecond, IReadOnlyList<ProcessMetric> Processes,
    IReadOnlyDictionary<string, double?> LogicalCpu, IReadOnlyList<DiskMetric> Disks, int? HardFaults)
{
    public IReadOnlyList<EtwProcessMetric> EtwProcesses { get; init; } = [];
    public int? ContextSwitches { get; init; }
    public ulong? TotalMemoryBytes { get; init; }
    public ulong? AvailableMemoryBytes { get; init; }
    public ulong? CommitBytes { get; init; }
    public ulong? CommitLimitBytes { get; init; }
    public IReadOnlyList<PhysicalDiskSample> PhysicalDisks { get; init; } = [];
    public ProcessSelectionCoverage? ProcessCoverage { get; init; }
}
public sealed record EtwProcessMetric(string Identifier, int CpuSamples, long ReadBytes, long WriteBytes, int Operations)
{ public double? ObservedStartSeconds { get; init; } public double? ObservedEndSeconds { get; init; } public string AttributionStatus { get; init; } = "ObservedEventsOnly;CheckSessionLoss"; }
public sealed record CollectionError(string Provider, string Code);
public sealed record DiagnosticSnapshot(DateTimeOffset StartedUtc, double DurationSeconds, string SourceProvider,
    IReadOnlyList<PerformanceSample> Samples, IReadOnlyList<CollectionError> Errors, long EventsReceived, long EventsLost,
    double CollectorCpuSeconds, long PeakWorkingSetBytes, double CollectionMilliseconds, string WorkloadContext,
    IReadOnlyList<SpaceMetric> StorageSpace, int? StartupEntries, double UptimeSeconds)
{
    public EtwStatistics? EtwStatistics { get; init; }
    public IReadOnlyList<ProcessLifetime> ProcessLifetimes { get; init; } = [];
    public IReadOnlyList<EtwProcessMetric> EtwContributions { get; init; } = [];
    public long ManagedAllocatedBytes { get; init; }
    public string RequestedEtwProfile { get; init; } = "General";
}
public sealed record Finding(string FindingId, string Category, string Evidence, string MeasurementWindow, int SampleCount,
    string SourceProvider, double Confidence, string Severity, string[] Limitations, string RecommendedNextCheck)
{
    public double EvidenceCoverage { get; init; }
    public double MeasurementQuality { get; init; }
    public double? Reproducibility { get; init; }
    public double? DiagnosticAccuracy { get; init; }
}
public sealed record PhysicalDiskSample(string Identifier, double? ReadBytesPerSecond, double? WriteBytesPerSecond,
    double? IoPerSecond, double? LatencyMilliseconds, double? Queue, double? ActivePercent,
    string DeviceType = "Unknown", string IdentityStatus = "SessionCounterInstanceOnly");
public sealed record ProcessSelectionCoverage(int Enumerated, int Accessible, int Selected, int AccessFailures, int MetadataOmitted);
public sealed record ProcessLifetime(string Identifier, DateTimeOffset StartedUtc, DateTimeOffset? EndedUtc, string EvidenceSource);
public sealed record EtwStatistics(long? NativeEtwEventsLost, long ApplicationQueueDropped, long MalformedEvents,
    long EventsAccepted, long EventsProcessed, long EventsPendingAtStop, string SessionCleanupStatus, string LossMeasurementStatus,
    string EffectiveProfile, bool RangeReduced, long UnattributedEvents, double MaxQueueDelayMilliseconds,
    int RequestedBufferMegabytes, int RequestedBufferQuantumKilobytes, int RequestedMinimumBuffers, int RequestedMaximumBuffers,
    IReadOnlyDictionary<string, long> ReceivedByKind);
public sealed record Distribution(double? Median, double? P95, int Count);
public static class Statistics
{
    public static Distribution Describe(IEnumerable<double?> values)
    {
        var sorted = values.Where(x => x.HasValue && double.IsFinite(x.Value)).Select(x => x!.Value).Order().ToArray();
        if (sorted.Length == 0) return new(null, null, 0);
        double Q(double p) { var index = (sorted.Length - 1) * p; var low = (int)index; return sorted[low] + (sorted[(int)Math.Ceiling(index)] - sorted[low]) * (index - low); }
        return new(Q(.5), Q(.95), sorted.Length);
    }
}
