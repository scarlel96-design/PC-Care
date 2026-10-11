namespace SmartPerformanceDoctor.Diagnostics;

public enum CpuActivity { Idle, Busy, Unknown }
public sealed record CpuExecutionInterval(int Processor, DateTimeOffset StartUtc, DateTimeOffset EndUtc, CpuActivity Activity);
public sealed record CpuReferenceBin(DateTimeOffset EndUtc, double BusySeconds, double IdleSeconds,
    double UnknownSeconds, double MissingSeconds, double OverlapSeconds, double? CpuPercent);
public sealed record CpuSchedulingSummary(int ProcessorCount, MeasurementWindow Window,
    IReadOnlyList<CpuReferenceBin> Bins, int InvalidIntervals, string Status);

/// <summary>Normalize SDK scheduling intervals. Keep missing time distinct from idle time.</summary>
public static class CpuSchedulingReference
{
    public static CpuSchedulingSummary Normalize(IEnumerable<CpuExecutionInterval> input, int processors, MeasurementWindow window)
    {
        if (processors is < 1 or > 256 || !window.Valid) throw new ArgumentException("InvalidCpuReferenceWindow");
        var intervals = input.Take(4_000_001).ToArray();
        if (intervals.Length > 4_000_000) throw new ArgumentException("CpuReferenceBudgetExceeded");
        int invalid = intervals.Count(x => x.Processor < 0 || x.Processor >= processors || x.EndUtc <= x.StartUtc
            || !Enum.IsDefined(x.Activity));
        var valid = intervals.Where(x => x.Processor >= 0 && x.Processor < processors && x.EndUtc > x.StartUtc
            && Enum.IsDefined(x.Activity)).GroupBy(x => x.Processor).ToDictionary(g => g.Key, g => g.OrderBy(x => x.StartUtc).ToArray());
        var bins = new List<CpuReferenceBin>();
        var offsets = new int[processors];
        for (int i = 0; i < Math.Floor(window.Seconds); i++)
        {
            var start = window.StartUtc.AddSeconds(i); var end = start.AddSeconds(1);
            double busy = 0, idle = 0, unknown = 0, missing = 0, overlap = 0;
            for (int cpu = 0; cpu < processors; cpu++)
            {
                var cursor = start;
                if (valid.TryGetValue(cpu, out var slices))
                {
                    while (offsets[cpu] < slices.Length && slices[offsets[cpu]].EndUtc <= start) offsets[cpu]++;
                    for (int position = offsets[cpu]; position < slices.Length; position++)
                    {
                        var slice = slices[position];
                        if (slice.EndUtc <= start) continue;
                        if (slice.StartUtc >= end) break;
                        var left = slice.StartUtc > start ? slice.StartUtc : start;
                        var right = slice.EndUtc < end ? slice.EndUtc : end;
                        if (left > cursor) missing += (left - cursor).TotalSeconds;
                        if (left < cursor) overlap += (Min(right, cursor) - left).TotalSeconds;
                        var effective = left > cursor ? left : cursor;
                        if (right <= effective) continue;
                        var seconds = (right - effective).TotalSeconds;
                        if (slice.Activity == CpuActivity.Busy) busy += seconds;
                        else if (slice.Activity == CpuActivity.Idle) idle += seconds;
                        else unknown += seconds;
                        cursor = right;
                    }
                }
                if (cursor < end) missing += (end - cursor).TotalSeconds;
            }
            // Require complete, non-overlapping coverage. Do not clamp invalid utilization.
            var complete = invalid == 0 && missing <= .000001 && unknown == 0 && overlap <= .000001;
            bins.Add(new(end, busy, idle, unknown, missing, overlap, complete ? 100 * busy / processors : null));
        }
        return new(processors, window, bins, invalid, bins.All(b => b.CpuPercent.HasValue) ? "CompleteSchedulingCoverage" : "ReferenceIncomplete");
    }
    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
