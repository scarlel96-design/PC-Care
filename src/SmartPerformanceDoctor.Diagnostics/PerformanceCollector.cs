using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace SmartPerformanceDoctor.Diagnostics;

public sealed class PerformanceCollector
{
    private static readonly SemaphoreSlim SessionGate = new(1, 1);
    public async Task<DiagnosticSnapshot> CollectAsync(SamplingPolicy policy, CancellationToken cancellationToken,
        IProgress<PerformanceSample>? progress = null, string workloadContext = "Unspecified")
    {
        policy.Validate();
        if (workloadContext is not ("Unspecified" or "ExpectedHighLoad" or "Idle")) throw new ArgumentException("Unknown workload context");
        if (!await SessionGate.WaitAsync(0, cancellationToken)) throw new InvalidOperationException("SessionConflict");
        try { return await Task.Run(() => CollectCoreAsync(policy, cancellationToken, progress, workloadContext), CancellationToken.None); }
        finally { SessionGate.Release(); }
    }
    private static async Task<DiagnosticSnapshot> CollectCoreAsync(SamplingPolicy policy, CancellationToken token,
        IProgress<PerformanceSample>? progress, string workload)
    {
        var started = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();
        using var self = Process.GetCurrentProcess();
        var ownCpu = self.TotalProcessorTime.TotalSeconds;
        var allocated = GC.GetTotalAllocatedBytes();
        var identities = new ProcessIdentityRegistry();
        var peak = self.WorkingSet64;
        var errors = new List<CollectionError>();
        var samples = new List<PerformanceSample>();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(policy.DurationSeconds));
        var native = new SystemSnapshotProbe();
        native.Read();
        var paths = new Dictionary<string, string> {
            ["latency"] = @"\PhysicalDisk(_Total)\Avg. Disk sec/Transfer",
            ["queue"] = @"\PhysicalDisk(_Total)\Current Disk Queue Length",
            ["read"] = @"\PhysicalDisk(_Total)\Disk Read Bytes/sec",
            ["write"] = @"\PhysicalDisk(_Total)\Disk Write Bytes/sec",
            ["iops"] = @"\PhysicalDisk(_Total)\Disk Transfers/sec",
            ["pageReads"] = @"\Memory\Page Reads/sec"
        };
        foreach (var (key, suffix) in new[] { ("latency", "Avg. Disk sec/Transfer"), ("queue", "Current Disk Queue Length"),
            ("read", "Disk Read Bytes/sec"), ("write", "Disk Write Bytes/sec"), ("iops", "Disk Transfers/sec"), ("active", "% Disk Time") })
            paths["perdisk." + key] = @"\PhysicalDisk(*)\" + suffix;
        for (var i = 0; i < Math.Min(Environment.ProcessorCount, 128); i++) paths["cpu" + i] = $@"\Processor({i})\% Processor Time";
        CounterProbe? counters = null;
        try { counters = new CounterProbe(paths); } catch (Exception e) { errors.Add(new("PDH", e.GetType().Name)); }
        await using var etw = new EtwCollector(policy.EventQueueCapacity, identities, policy.EtwProfile, started);
        var initialProcesses = Process.GetProcesses();
        try { foreach (var process in initialProcesses.Take(8192))
            try { identities.ObserveSnapshot(process.Id, new(process.StartTime.ToUniversalTime()), DateTimeOffset.UtcNow); } catch { } }
        finally { foreach (var process in initialProcesses) process.Dispose(); }
        etw.Start();
        var previous = new Dictionary<string, (double Cpu, double Seconds)>();
        var salt = Guid.NewGuid().ToString("N");
        var space = new List<SpaceMetric>();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).Take(64))
            try { if (drive.TotalSize > 0) space.Add(new(drive.Name[..1], 100d * drive.AvailableFreeSpace / drive.TotalSize)); }
            catch (Exception e) { errors.Add(new("StorageSpace", e.GetType().Name)); }
        int? startup = 0;
        try
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                using var key = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: false);
                startup += key?.ValueCount ?? 0;
            }
        }
        catch (Exception e) { startup = null; errors.Add(new("StartupInventory", e.GetType().Name)); }
        try
        {
            while (samples.Count < 120)
            {
                var interval = samples.LastOrDefault()?.CpuPercent >= 90 ? Math.Max(2000, policy.IntervalMilliseconds) : policy.IntervalMilliseconds;
                await Task.Delay(interval, deadline.Token);
                var seconds = clock.Elapsed.TotalSeconds;
                var system = native.Read();
                IReadOnlyDictionary<string, double?> values = new Dictionary<string, double?>();
                try { values = counters?.Read() ?? values; } catch (Exception e) { if (!errors.Any(x => x.Provider == "PDH")) errors.Add(new("PDH", e.GetType().Name)); }
                double? Value(string name) => values.GetValueOrDefault(name);
                var processMetrics = new List<ProcessMetric>();
                var failures = 0;
                var current = new Dictionary<string, (double Cpu, double Seconds)>();
                // Dispose every process handle, including entries beyond the configured budget.
                var processes = Process.GetProcesses();
                try
                {
                    foreach (var process in processes.Take(8192))
                    {
                        try
                        {
                            var identity = identities.ObserveSnapshot(process.Id, new(process.StartTime.ToUniversalTime()), DateTimeOffset.UtcNow);
                            if (identity is null) { failures++; continue; }
                            var cpuTime = process.TotalProcessorTime.TotalSeconds;
                            double? cpu = previous.TryGetValue(identity, out var old) && seconds > old.Seconds && cpuTime >= old.Cpu
                                ? 100 * (cpuTime - old.Cpu) / (seconds - old.Seconds) / Environment.ProcessorCount : null;
                            var identifier = identity;
                            processMetrics.Add(new(identifier, cpu is >= 0 and <= 100 ? cpu : null, process.WorkingSet64, cpuTime));
                            current[identity] = (cpuTime, seconds);
                        }
                        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                        { failures++; if (!errors.Any(x => x.Provider == "Process")) errors.Add(new("Process", "PartialAccessOrExited")); }
                    }
                    if (processes.Length > policy.MaxProcesses && !errors.Any(x => x.Provider == "ProcessBudget")) errors.Add(new("ProcessBudget", "Truncated"));
                }
                finally { foreach (var process in processes) process.Dispose(); }
                var selected = ProcessSelection.Select(processMetrics, policy.MaxProcesses);
                var coverage = new ProcessSelectionCoverage(processes.Length, processMetrics.Count, selected.Count, failures, Math.Max(0, processes.Length - 8192));
                previous = current;
                var (disks, faults, etwProcesses, switches) = etw.Drain();
                var sample = new PerformanceSample(seconds, Environment.ProcessorCount <= 64 ? system.Cpu : null,
                    system.TotalMemory > 0 ? 100d * system.AvailableMemory / system.TotalMemory : null,
                    system.CommitLimit > 0 ? 100d * system.Commit / system.CommitLimit : null,
                    Value("latency") * 1000, Value("queue"), Value("read"), Value("write"), Value("iops"), Value("pageReads"),
                    selected, values.Where(v => v.Key.StartsWith("cpu", StringComparison.Ordinal)).ToDictionary(), disks,
                    policy.EtwProfile == "Detailed" && etw.Error is null && !etw.RangeReduced ? faults : null) { EtwProcesses = etwProcesses, ContextSwitches = policy.EtwProfile == "Detailed" && etw.Error is null && !etw.RangeReduced ? switches : null,
                    TotalMemoryBytes = system.TotalMemory, AvailableMemoryBytes = system.AvailableMemory,
                    CommitBytes = system.Commit, CommitLimitBytes = system.CommitLimit, ProcessCoverage = coverage,
                    PhysicalDisks = PerDiskNormalizer.From(values, salt) };
                sample = Normalizer.Normalize(sample);
                samples.Add(sample);
                await etw.ReduceIfOverloadedAsync();
                progress?.Report(sample);
                self.Refresh(); peak = Math.Max(peak, self.WorkingSet64);
            }
        }
        catch (OperationCanceledException) { if (token.IsCancellationRequested) errors.Add(new("Collector", "Cancelled")); }
        catch (Exception e) { errors.Add(new("Collector", e.GetType().Name)); }
        finally { counters?.Dispose(); }
        await etw.DisposeAsync();
        if (etw.Error is not null) errors.Add(etw.Error);
        if (identities.Truncated) errors.Add(new("ProcessIdentity", "LifetimeBudgetExceeded"));
        if (!samples.Any(s => s.CpuPercent.HasValue)) errors.Add(new("SystemCpu", "Unavailable"));
        if (!samples.Any(s => s.AvailableMemoryPercent.HasValue)) errors.Add(new("PhysicalMemory", "Unavailable"));
        if (!samples.Any(s => s.CommitPercent.HasValue)) errors.Add(new("CommitMemory", "Unavailable"));
        if (Environment.ProcessorCount > 64) errors.Add(new("SystemCpu", "ProcessorGroupCoverageUnknown"));
        if (!samples.Any(s => s.DiskLatencyMilliseconds.HasValue)) errors.Add(new("PDH.Disk", "Unavailable"));
        if (!samples.Any(s => s.LogicalCpu.Values.Any(v => v.HasValue))) errors.Add(new("PDH.LogicalCpu", "Unavailable"));
        self.Refresh();
        return new(started, clock.Elapsed.TotalSeconds, "CsWin32.SystemSnapshot+PDH+TraceEvent", samples, errors, etw.Received, etw.Lost,
            self.TotalProcessorTime.TotalSeconds - ownCpu, peak, clock.Elapsed.TotalMilliseconds, workload, space, startup, Environment.TickCount64 / 1000d) { EtwStatistics = etw.GetStatistics(), ProcessLifetimes = identities.Export(),
                EtwContributions = etw.ExportContributions(), ManagedAllocatedBytes = GC.GetTotalAllocatedBytes() - allocated, RequestedEtwProfile = policy.EtwProfile };
    }
}
