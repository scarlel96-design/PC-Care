using SmartPerformanceDoctor.Diagnostics;
using Microsoft.Diagnostics.Tracing.Session;
using System.Text.Json;
if (args.FirstOrDefault() is "--workload" or "--qualification-workload") return await ControlledWorkload.Run(args);
if (args.FirstOrDefault() == "--disk-identities")
{
    if (args.Length != 4) return 2;
    var identities = JsonSerializer.Deserialize<StorageIdentity[]>(File.ReadAllText(args[1]), new JsonSerializerOptions { PropertyNameCaseInsensitive=true }) ?? [];
    var key = File.ReadAllBytes(args[2]);
    using var counters = new CounterProbe([new("perdisk.latency", @"\PhysicalDisk(*)\Avg. Disk sec/Transfer")]);
    await Task.Delay(1000);
    var values = counters.Read();
    var disks = values.Keys.Select(k=>k[(k.IndexOf(':')+1)..]).Distinct().Select(i=>PhysicalDiskMapping.Resolve(i,identities,key)).ToArray();
    await File.WriteAllTextAsync(args[3],JsonSerializer.Serialize(new { status=disks.Length>0?"PARTIAL":"NOT TESTED", disks, limitations="Read-only number/UniqueId correlation; no topology-change or complete device-type qualification" },new JsonSerializerOptions{WriteIndented=true}));
    System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
    Console.WriteLine("DISK_IDENTITY_OBSERVED devices="+disks.Length);
    return 0;
}
var profile = args.Length > 2 ? args[2] : "General";
var seconds = args.Length > 0 && int.TryParse(args[0], out var duration) ? duration : 10;
HashSet<string>? sessionBaseline = null;
if (TraceEventSession.IsElevated() == true)
    try { sessionBaseline = TraceEventSession.GetActiveSessionNames().ToHashSet(StringComparer.Ordinal); } catch { }
var collector = new PerformanceCollector();
var result = await collector.CollectAsync(new SamplingPolicy(DurationSeconds: seconds) { EtwProfile = profile }, CancellationToken.None);
int? remainingOwnedSessions = null;
if (sessionBaseline is not null)
    try { remainingOwnedSessions = TraceEventSession.GetActiveSessionNames().Count(n => n.StartsWith("PCCare.Diagnostics.", StringComparison.Ordinal) && !sessionBaseline.Contains(n)); } catch { }
var analysisClock = System.Diagnostics.Stopwatch.StartNew();
var findings = BottleneckAnalyzer.Analyze(result);
var measurements = MeasurementSummary.From(result);
analysisClock.Stop();
var json = JsonSerializer.Serialize(new {
    environment = new { os = Environment.OSVersion.ToString(), logicalProcessors = Environment.ProcessorCount,
        traceEvent = typeof(TraceEventSession).Assembly.GetName().Version?.ToString(), elevated = TraceEventSession.IsElevated() },
    snapshot = result, findings, measurements, remainingOwnedSessions, analysisMilliseconds = analysisClock.Elapsed.TotalMilliseconds,
    cpu = Statistics.Describe(result.Samples.Select(s => s.CpuPercent)),
    memory = Statistics.Describe(result.Samples.Select(s => s.AvailableMemoryPercent)),
    overheadPercentOfMachine = 100 * result.CollectorCpuSeconds / result.DurationSeconds / Environment.ProcessorCount
}, new JsonSerializerOptions { WriteIndented = true });
if (args.Length > 1) await File.WriteAllTextAsync(args[1], json);
else Console.WriteLine(json);
return result.Samples.Any(s => s.CpuPercent.HasValue && s.AvailableMemoryPercent.HasValue) ? 0 : 1;
