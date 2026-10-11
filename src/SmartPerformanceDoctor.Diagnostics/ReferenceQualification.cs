namespace SmartPerformanceDoctor.Diagnostics;

public sealed record MeasurementWindow(DateTimeOffset StartUtc, DateTimeOffset EndUtc)
{
    public double Seconds => (EndUtc - StartUtc).TotalSeconds;
    public bool Valid => Seconds is >= 10 and <= 120;
}
public sealed record ReferencePoint(DateTimeOffset EndUtc, double? CpuPercent);
public sealed record IndependentTrace(string Analyzer, bool IndependentParser, long? LostEvents, long? LostBuffers,
    long CpuGaps, MeasurementWindow Window, IReadOnlyList<ReferencePoint> Cpu);
public sealed record QualificationResult(string Status, MeasurementWindow? Window, AccuracyMetrics Metrics,
    IReadOnlyList<string> Exclusions, double? CpuMedianAbsoluteError, string PressureState);

public static class ReferenceQualification
{
    public static DiagnosticSnapshot Crop(DiagnosticSnapshot snapshot, MeasurementWindow window)
    {
        if (!window.Valid || window.StartUtc < snapshot.StartedUtc
            || window.EndUtc > snapshot.StartedUtc.AddSeconds(snapshot.DurationSeconds)) throw new ArgumentException("InvalidMeasurementWindow");
        // A sample describes an interval. Exclude intervals that cross either window boundary.
        var result = new List<PerformanceSample>();
        var previous = snapshot.StartedUtc;
        foreach (var sample in snapshot.Samples)
        {
            var end = snapshot.StartedUtc.AddSeconds(sample.Seconds);
            if (previous >= window.StartUtc && end <= window.EndUtc && end > previous)
                result.Add(sample with { Seconds = (end - window.StartUtc).TotalSeconds });
            previous = end;
        }
        return snapshot with { StartedUtc = window.StartUtc, DurationSeconds = window.Seconds, Samples = result,
            EtwContributions = [], ProcessLifetimes = [] };
    }

    public static QualificationResult Evaluate(DiagnosticSnapshot snapshot, IndependentTrace reference, MeasurementWindow workload)
    {
        var exclusions = new List<string>();
        var window = new MeasurementWindow(new[] { snapshot.StartedUtc, reference.Window.StartUtc, workload.StartUtc }.Max(),
            new[] { snapshot.StartedUtc.AddSeconds(snapshot.DurationSeconds), reference.Window.EndUtc, workload.EndUtc }.Min());
        if (!reference.IndependentParser || reference.Analyzer != "Microsoft.TraceProcessing") exclusions.Add("IndependentParserNotVerified");
        if (reference.LostEvents != 0 || reference.LostBuffers != 0 || reference.CpuGaps != 0) exclusions.Add("ReferenceLossOrUnknownLoss");
        if (!window.Valid) exclusions.Add("InvalidOrShortCommonWindow");
        if (exclusions.Count > 0) return new("NOT TESTED", window.Valid ? window : null,
            ReferenceComparison.Evaluate([], [new("CpuSaturation", null), new("MemoryPressure", null), new("DiskIoPressure", null)], false, false), exclusions, null, "Unknown");
        var points = reference.Cpu.Where(p => p.EndUtc.AddSeconds(-1) >= window.StartUtc && p.EndUtc <= window.EndUtc).ToArray();
        var ordered = points.Length >= 8 && points.Zip(points.Skip(1)).All(p => (p.Second.EndUtc - p.First.EndUtc).TotalSeconds is > .5 and < 1.5);
        var valid = ordered && points.All(p => p.CpuPercent is >= 0 and <= 100 && double.IsFinite(p.CpuPercent.Value))
            && points.Length >= .8 * Math.Floor(window.Seconds);
        if (!valid) return new("PARTIAL", window, ReferenceComparison.Evaluate([], [new("CpuSaturation", null)], true, true),
            ["ReferenceCpuCoverageInsufficient"], null, "Unknown");
        var high = points.Count(p => p.CpuPercent >= 90);
        var run = 0; var longest = 0;
        foreach (var point in points) { run = point.CpuPercent >= 90 ? run + 1 : 0; longest = Math.Max(longest, run); }
        bool pressure = high >= .8 * points.Length && longest >= 8;
        var cropped = Crop(snapshot, window);
        var metrics = ReferenceComparison.Evaluate(BottleneckAnalyzer.Analyze(cropped),
            [new("CpuSaturation", pressure), new("MemoryPressure", null), new("DiskIoPressure", null)], true, true);
        var errors = new List<double?>();
        var prev = window.StartUtc;
        foreach (var sample in cropped.Samples)
        {
            var end = window.StartUtc.AddSeconds(sample.Seconds);
            var aligned = points.Where(p => p.EndUtc > prev && p.EndUtc <= end).Select(p => p.CpuPercent!.Value).ToArray();
            if (sample.CpuPercent.HasValue && aligned.Length > 0) errors.Add(Math.Abs(sample.CpuPercent.Value - aligned.Average()));
            prev = end;
        }
        return new("PARTIAL", window, metrics, ["MemoryAndDiskPressureHaveNoIndependentTruth", "OneRunnerDoesNotQualifyAllWindowsDevices"],
            Statistics.Describe(errors).Median, pressure ? "PressureVerified" : "NoSustainedCpuPressureObserved");
    }
}
