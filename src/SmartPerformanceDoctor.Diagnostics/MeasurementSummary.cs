namespace SmartPerformanceDoctor.Diagnostics;

public sealed record ProcessDistribution(string Identifier, Distribution CpuPercent, Distribution WorkingSetBytes);
public sealed record MeasurementSummary(Distribution CpuPercent, Distribution DiskLatencyMilliseconds,
    double ObservedMemoryPressureSeconds, IReadOnlyList<ProcessDistribution> Processes)
{
    public static MeasurementSummary From(DiagnosticSnapshot snapshot)
    {
        var samples = snapshot.Samples.Select(Normalizer.Normalize).ToArray();
        static bool Pressured(PerformanceSample sample) => sample.AvailableMemoryPercent.HasValue && sample.CommitPercent.HasValue
            && Math.Max(100 - sample.AvailableMemoryPercent.Value, sample.CommitPercent.Value) >= 90;
        // Count only adjacent observed intervals. Do not interpolate across missing measurements.
        var pressureSeconds = samples.Zip(samples.Skip(1))
            .Where(p => double.IsFinite(p.First.Seconds) && double.IsFinite(p.Second.Seconds)
                && p.First.Seconds > 0 && p.Second.Seconds <= snapshot.DurationSeconds + .5
                && p.Second.Seconds > p.First.Seconds && p.Second.Seconds - p.First.Seconds <= 5.5
                && Pressured(p.First) && Pressured(p.Second))
            .Sum(p => p.Second.Seconds - p.First.Seconds);
        var processes = samples.SelectMany(s => s.Processes).GroupBy(p => p.Identifier)
            .Select(g => new ProcessDistribution(g.Key, Statistics.Describe(g.Select(p => Normalizer.Bounded(p.CpuPercent, 0, 100))),
                Statistics.Describe(g.Select(p => p.WorkingSetBytes >= 0 ? (double?)p.WorkingSetBytes : null))))
            .OrderByDescending(p => p.CpuPercent.Median).Take(128).ToArray();
        return new(Statistics.Describe(samples.Select(s => s.CpuPercent)),
            Statistics.Describe(samples.Select(s => s.DiskLatencyMilliseconds)), pressureSeconds, processes);
    }
}
