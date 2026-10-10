using System.Security.Cryptography;
using System.Text;

namespace SmartPerformanceDoctor.Diagnostics;

public static class PerDiskNormalizer
{
    public static IReadOnlyList<PhysicalDiskSample> From(IReadOnlyDictionary<string, double?> values, string sessionSalt)
    {
        var groups = values.Where(x => x.Key.StartsWith("perdisk.", StringComparison.Ordinal) && x.Key.Contains(':'))
            .GroupBy(x => x.Key[(x.Key.IndexOf(':') + 1)..]).Take(64);
        return groups.Select(g =>
        {
            double? Get(string metric, double max) => Normalizer.Bounded(g.FirstOrDefault(x => x.Key.StartsWith("perdisk." + metric + ":", StringComparison.Ordinal)).Value, 0, max);
            var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sessionSalt + ":disk:" + g.Key)))[..24];
            return new PhysicalDiskSample(id, Get("read", 1e15), Get("write", 1e15), Get("iops", 1e12),
                Get("latency", 60) * 1000, Get("queue", 1e6), Get("active", 1e6));
        }).ToArray();
    }
}

public sealed record DiskBaseline(string Identifier, string Workload, Distribution Latency, int SampleCount);
public sealed record DiskComparison(string Status, double? MedianChangeMilliseconds, string Limitation);
public static class DiskCalibration
{
    public static DiskComparison Compare(DiskBaseline baseline, DiskBaseline measurement)
    {
        if (baseline.Identifier != measurement.Identifier || baseline.Workload != measurement.Workload)
            return new("Unknown", null, "Device identity or workload differs. Session identifiers cannot identify hardware across sessions.");
        if (baseline.SampleCount < 8 || measurement.SampleCount < 8 || baseline.Latency.Median is null || measurement.Latency.Median is null)
            return new("InsufficientEvidence", null, "At least eight valid measurements are required.");
        return new("ConfirmedObservation", measurement.Latency.Median - baseline.Latency.Median,
            "A latency change does not identify a root cause. Hardware type remains Unknown.");
    }
}
