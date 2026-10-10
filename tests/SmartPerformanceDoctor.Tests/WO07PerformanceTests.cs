using SmartPerformanceDoctor.Diagnostics;
using SmartPerformanceDoctor.App.Services.SystemCare;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WO07PerformanceTests
{
    private static DiagnosticSnapshot Fixture(Func<int, PerformanceSample> make, string workload = "Unspecified", long lost = 0) =>
        new(DateTimeOffset.Parse("2026-10-11T00:00:00Z"), 30, "Synthetic", Enumerable.Range(1, 30).Select(make).ToArray(), [], 100, lost, 0, 0, 0, workload, [], 0, 0);
    private static PerformanceSample S(int i, double? cpu = 10, double? available = 60, double? commit = 40, double? latency = 1,
        double? queue = 0, double? iops = 0) => new(i, cpu, available, commit, latency, queue, 1000, 1000, iops, 0, [], new Dictionary<string,double?>(), [], 0);
    [Fact] public void SustainedCpuIsCandidate() => Assert.Contains(BottleneckAnalyzer.Analyze(Fixture(i => S(i, 95))), f => f.Category == "CpuSaturation" && f.Severity == "Candidate" && f.Confidence == 1);
    [Fact] public void SinglePeakIsTransient() => Assert.DoesNotContain(BottleneckAnalyzer.Analyze(Fixture(i => S(i, i == 15 ? 99 : 10))), f => f.Category == "CpuSaturation");
    [Theory] [InlineData(1)] [InlineData(1000)] public void ThroughputAloneIsNotDiskBottleneck(double bytes) => Assert.DoesNotContain(BottleneckAnalyzer.Analyze(Fixture(i => S(i) with { ReadBytesPerSecond = bytes })), f => f.Category == "DiskIoPressure");
    [Theory] [InlineData(30, 2, 200)] [InlineData(80, 4, 10)] public void SequentialAndRandomPressureRequireLatencyQueueAndOperations(double latency, double queue, double ops) => Assert.Contains(BottleneckAnalyzer.Analyze(Fixture(i => S(i, latency: latency, queue: queue, iops: ops))), f => f.Category == "DiskIoPressure");
    [Fact] public void SustainedMemoryPressure() => Assert.Contains(BottleneckAnalyzer.Analyze(Fixture(i => S(i, available: 5, commit: 95))), f => f.Category == "MemoryPressure");
    [Fact] public void ExpectedWorkloadIsNotAbnormal() => Assert.Contains(BottleneckAnalyzer.Analyze(Fixture(i => S(i, 98), "ExpectedHighLoad")), f => f.Category == "CpuSaturation" && f.Severity == "ExpectedWorkload");
    [Fact] public void MixedProcessesHaveStableContribution() => Assert.Contains(BottleneckAnalyzer.Analyze(Fixture(i => S(i, 95) with { Processes = [new("fixtureA", 55, 100, i), new("fixtureB", 25, 100, i)] })), f => f.Category == "ProcessContribution.fixtureA");
    [Fact] public void LostEventsExposeLimitations() => Assert.All(BottleneckAnalyzer.Analyze(Fixture(i => S(i), lost: 10)), f => Assert.Contains(f.Limitations, l => l.Contains("lost")));
    [Fact] public void MissingCountersRemainUnknown() => Assert.Contains(BottleneckAnalyzer.Analyze(Fixture(i => S(i, latency: null))), f => f.Category == "InsufficientEvidence.DiskIoPressure" && f.Confidence == 0);
    [Theory] [InlineData(-1)] [InlineData(101)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] public void InvalidCpuRejected(double value) => Assert.Contains(BottleneckAnalyzer.Analyze(Fixture(i => S(i, value))), f => f.Category == "InsufficientEvidence.CpuSaturation");
    [Theory] [InlineData(-1)] [InlineData(double.NaN)] public void InvalidDiskQueueRemainsUnknown(double queue) => Assert.Contains(BottleneckAnalyzer.Analyze(Fixture(i => S(i, latency: 30, queue: queue, iops: 10))), f => f.Category == "InsufficientEvidence.DiskIoPressure");
    [Fact] public void InvalidCommitRemainsUnknown() => Assert.Contains(BottleneckAnalyzer.Analyze(Fixture(i => S(i, available: 5, commit: -1))), f => f.Category == "InsufficientEvidence.MemoryPressure");
    [Fact] public void ReversedTimeRejected() => Assert.All(BottleneckAnalyzer.Analyze(Fixture(i => S(i, 95) with { Seconds = 31 - i })), f => Assert.Equal(0, f.Confidence));
    [Fact] public void ShortWindowIsInsufficient() => Assert.Contains(BottleneckAnalyzer.Analyze(Fixture(i => S(i, 95)) with { DurationSeconds = 2 }), f => f.Severity == "Unknown");
    [Fact] public void SparseMeasurementsAreInsufficient() => Assert.Contains(BottleneckAnalyzer.Analyze(Fixture(i => S(i, i < 5 ? 95 : null))), f => f.Category == "InsufficientEvidence.CpuSaturation");
    [Fact] public void LongMissingGapDoesNotBecomeSustained() => Assert.DoesNotContain(BottleneckAnalyzer.Analyze(Fixture(i => S(i, i % 5 == 0 ? null : 95))), f => f.Category == "CpuSaturation");
    [Fact] public void AdapterNeverProvidesRepair() => Assert.All(PerformanceFindingAdapter.Convert(Fixture(i => S(i, 95))), f => { Assert.False(f.CanAutoApply); Assert.Null(f.TargetPath); Assert.NotEmpty(f.Evidence); });
    [Fact] public void ResourcePolicyRejectsUnboundedSettings() => Assert.Throws<ArgumentOutOfRangeException>(() => new SamplingPolicy(DurationSeconds: 121).Validate());
    [Fact] public void StatisticsUseInterpolation() { var d = Statistics.Describe([1, 2, 3, 4]); Assert.Equal(2.5, d.Median); Assert.Equal(3.85, d.P95!.Value, 5); }
    [Fact] public void EmptyDistributionIsUnknown() => Assert.Null(Statistics.Describe([]).Median);
    [Fact] public void QueueOverflowIsMeasured() { var buffer = new EventBuffer(32); for (var i = 0; i < 40; i++) buffer.Offer(new(0, 1, false, 1, false)); Assert.Equal(40, buffer.Received); Assert.Equal(8, buffer.Dropped); Assert.Equal(32, buffer.Drain().Count); }
    [Fact] public void MalformedEventsAreDropped() { var buffer = new EventBuffer(32); buffer.Offer(new(0, -1, false, double.NaN, false)); Assert.Empty(buffer.Drain()); Assert.Equal(1, buffer.Dropped); }
    [Fact] public void NormalizerRejectsCounterSentinels() { var sample = Normalizer.Normalize(S(1, double.NaN, latency: -1) with { ReadBytesPerSecond = double.PositiveInfinity }); Assert.Null(sample.CpuPercent); Assert.Null(sample.DiskLatencyMilliseconds); Assert.Null(sample.ReadBytesPerSecond); }
    [Fact] public void ErrorReporterDoesNotLeakExceptionPaths() => Assert.Equal(new CollectionError("fixture", "PermissionDenied"), ErrorReporter.From("fixture", new UnauthorizedAccessException(@"C:\Users\secret\vault")));
    private sealed class ThrowProgress : IProgress<PerformanceSample> { public void Report(PerformanceSample value) => throw new IOException("synthetic observer failure"); }
    [Fact] public async Task CollectorExceptionPreservesEvidence()
    {
        var result = await new PerformanceCollector().CollectAsync(new(DurationSeconds: 30), CancellationToken.None, new ThrowProgress());
        Assert.Contains(result.Errors, e => e.Provider == "Collector" && e.Code == "IOException");
        Assert.Single(result.Samples);
        Assert.All(BottleneckAnalyzer.Analyze(result), f => Assert.Equal(0, f.Confidence));
    }
    [Fact] public async Task CancellationAndSessionConflictAreContained()
    {
        using var cancel = new CancellationTokenSource();
        var collector = new PerformanceCollector();
        var first = collector.CollectAsync(new(DurationSeconds: 5), cancel.Token);
        await Task.Delay(100);
        await Assert.ThrowsAsync<InvalidOperationException>(() => collector.CollectAsync(new(DurationSeconds: 5), CancellationToken.None));
        cancel.Cancel();
        var snapshot = await first;
        Assert.Contains(snapshot.Errors, e => e.Code == "Cancelled");
        using var again = new CancellationTokenSource(); again.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => collector.CollectAsync(new(), again.Token));
    }
}
