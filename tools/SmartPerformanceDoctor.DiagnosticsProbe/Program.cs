using SmartPerformanceDoctor.Diagnostics;
using Microsoft.Diagnostics.Tracing.Session;
using System.Text.Json;
var seconds = args.Length > 0 && int.TryParse(args[0], out var duration) ? duration : 10;
var collector = new PerformanceCollector();
var result = await collector.CollectAsync(new(DurationSeconds: seconds), CancellationToken.None);
var analysisClock = System.Diagnostics.Stopwatch.StartNew();
var findings = BottleneckAnalyzer.Analyze(result);
analysisClock.Stop();
var json = JsonSerializer.Serialize(new {
    environment = new { os = Environment.OSVersion.ToString(), logicalProcessors = Environment.ProcessorCount,
        traceEvent = typeof(TraceEventSession).Assembly.GetName().Version?.ToString(), elevated = TraceEventSession.IsElevated() },
    snapshot = result, findings, analysisMilliseconds = analysisClock.Elapsed.TotalMilliseconds,
    cpu = Statistics.Describe(result.Samples.Select(s => s.CpuPercent)),
    memory = Statistics.Describe(result.Samples.Select(s => s.AvailableMemoryPercent)),
    overheadPercentOfMachine = 100 * result.CollectorCpuSeconds / result.DurationSeconds / Environment.ProcessorCount
}, new JsonSerializerOptions { WriteIndented = true });
if (args.Length > 1) await File.WriteAllTextAsync(args[1], json);
else Console.WriteLine(json);
return result.Samples.Any(s => s.CpuPercent.HasValue && s.AvailableMemoryPercent.HasValue) ? 0 : 1;
