namespace SmartPerformanceDoctor.Diagnostics;

public static class BottleneckAnalyzer
{
    public static IReadOnlyList<Finding> Analyze(DiagnosticSnapshot snapshot)
    {
        var results = new List<Finding>();
        var samples = snapshot.Samples.Select(Normalizer.Normalize).ToList();
        var validTime = samples.Count > 0 && samples.All(s => double.IsFinite(s.Seconds) && s.Seconds > 0 && s.Seconds <= snapshot.DurationSeconds + .5)
            && samples.Zip(samples.Skip(1)).All(pair => pair.Second.Seconds > pair.First.Seconds);
        var baseLimits = new List<string> { "Resource pressure does not prove abnormal software.", "Repeat the same workload before planning a repair.",
            "Clock, temperature, boot attribution and service correlation are not measured." };
        if (snapshot.Errors.Count > 0) baseLimits.Add("Collection errors: " + string.Join(",", snapshot.Errors.Select(e => e.Provider + ":" + e.Code)));
        if (snapshot.EventsLost > 0) baseLimits.Add("ETW events were lost; event attribution is incomplete.");
        void Add(string category, string evidence, int count, double coverage, string severity = "Observation")
        {
            var span = samples.Count > 1 ? samples[^1].Seconds - samples[0].Seconds : 0;
            var confidence = Math.Clamp(coverage * Math.Min(1, count / 20d) * Math.Min(1, span / 20d), 0, 1);
            if (!validTime) confidence = 0;
            var provider = snapshot.SourceProvider == "Synthetic" ? "Synthetic" :
                category.Contains("CpuSaturation", StringComparison.Ordinal) ? "CsWin32.GetSystemTimes" :
                category.Contains("MemoryPressure", StringComparison.Ordinal) ? "CsWin32.GlobalMemoryStatusEx+GetPerformanceInfo" :
                category.Contains("DiskIoPressure", StringComparison.Ordinal) ? "CsWin32.PDH" : "System.Diagnostics.Process";
            results.Add(new("perf." + category, category, evidence, $"{snapshot.StartedUtc:O} + {snapshot.DurationSeconds:F2}s", count,
                provider, confidence, severity, baseLimits.ToArray(), "Repeat measurement and identify the active workload; no automatic repair."));
        }
        bool Check(string category, Func<PerformanceSample, double?> select, Func<double, bool> predicate, Func<double, bool> valid)
        {
            var values = samples.Select(select).ToArray();
            var measured = values.Count(v => v.HasValue && double.IsFinite(v.Value) && valid(v.Value));
            var coverage = samples.Count == 0 ? 0 : (double)measured / samples.Count;
            var matching = values.Count(v => v.HasValue && double.IsFinite(v.Value) && valid(v.Value) && predicate(v.Value));
            if (!validTime || measured < 8 || coverage < .8 || snapshot.DurationSeconds < 10)
            { Add("InsufficientEvidence." + category, $"Valid measurements {measured}/{samples.Count}; coverage {coverage:P1}.", measured, 0, "Unknown"); return false; }
            var continuousSeconds = 0d; var longest = 0d;
            for (var i = 1; i < samples.Count; i++)
            {
                var dt = samples[i].Seconds - samples[i - 1].Seconds;
                var a = values[i - 1]; var b = values[i];
                if (dt <= 5.5 && a.HasValue && b.HasValue && double.IsFinite(a.Value) && double.IsFinite(b.Value)
                    && valid(a.Value) && valid(b.Value) && predicate(a.Value) && predicate(b.Value)) continuousSeconds += dt;
                else continuousSeconds = 0;
                longest = Math.Max(longest, continuousSeconds);
            }
            var distribution = Statistics.Describe(values.Where(v => v.HasValue && valid(v.Value)));
            var evidence = $"Median {distribution.Median:F2}; P95 {distribution.P95:F2}; matching {matching}/{measured}; longest continuous {longest:F2}s; coverage {coverage:P1}.";
            if (matching >= .8 * measured && longest >= 8)
            { Add(category, evidence, measured, coverage, snapshot.WorkloadContext == "ExpectedHighLoad" ? "ExpectedWorkload" : "Candidate"); return true; }
            Add(matching > 0 ? "Transient." + category : "NoObservedPressure." + category, evidence, measured, coverage);
            return false;
        }
        var cpu = Check("CpuSaturation", s => s.CpuPercent, x => x >= 90, x => x is >= 0 and <= 100);
        Check("MemoryPressure", s => s.AvailableMemoryPercent.HasValue && s.CommitPercent.HasValue ? Math.Max(100 - s.AvailableMemoryPercent.Value, s.CommitPercent.Value) : null,
            x => x >= 90, x => x is >= 0 and <= 100);
        Check("DiskIoPressure", s => s.DiskQueue >= 1 && s.IoPerSecond > 0 ? s.DiskLatencyMilliseconds :
            s.DiskLatencyMilliseconds.HasValue && s.DiskQueue.HasValue && s.IoPerSecond.HasValue ? 0 : null,
            x => x >= 20, x => x >= 0 && x < 60000);
        if (cpu)
        {
            var contributors = samples.SelectMany(s => s.Processes.Select(p => (s.Seconds, Metric: p)))
                .GroupBy(x => x.Metric.Identifier).Select(g => new { Id = g.Key, Count = g.Count(), Cpu = Statistics.Describe(g.Select(x => x.Metric.CpuPercent)) });
            foreach (var p in contributors.Where(p => p.Count >= samples.Count * .8 && p.Cpu.Median >= 20).Take(8))
                Add("ProcessContribution." + p.Id, $"Process {p.Id}; CPU median {p.Cpu.Median:F2}% of all logical processors.", p.Count, (double)p.Count / samples.Count);
        }
        return results;
    }
}
