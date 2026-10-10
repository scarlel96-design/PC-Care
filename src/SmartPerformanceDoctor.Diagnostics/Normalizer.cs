namespace SmartPerformanceDoctor.Diagnostics;

public static class Normalizer
{
    public static double? Bounded(double? value, double minimum, double maximum) =>
        value.HasValue && double.IsFinite(value.Value) && value >= minimum && value <= maximum ? value : null;
    public static PerformanceSample Normalize(PerformanceSample s) => s with
    {
        CpuPercent = Bounded(s.CpuPercent, 0, 100), AvailableMemoryPercent = Bounded(s.AvailableMemoryPercent, 0, 100),
        CommitPercent = Bounded(s.CommitPercent, 0, 100), DiskLatencyMilliseconds = Bounded(s.DiskLatencyMilliseconds, 0, 60000),
        DiskQueue = Bounded(s.DiskQueue, 0, 1000000), ReadBytesPerSecond = Bounded(s.ReadBytesPerSecond, 0, 1e15),
        WriteBytesPerSecond = Bounded(s.WriteBytesPerSecond, 0, 1e15), IoPerSecond = Bounded(s.IoPerSecond, 0, 1e12),
        PageReadsPerSecond = Bounded(s.PageReadsPerSecond, 0, 1e12),
        LogicalCpu = s.LogicalCpu.ToDictionary(p => p.Key, p => Bounded(p.Value, 0, 100))
    };
}
