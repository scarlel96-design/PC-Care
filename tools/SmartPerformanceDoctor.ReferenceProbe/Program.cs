using Microsoft.Windows.EventTracing;
using Microsoft.Windows.EventTracing.Processes;
using SmartPerformanceDoctor.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

if (args is ["--verify-time-contract"]) return SdkTimeConversion.VerifyContract() ? 0 : 1;

if (args.Length != 5) { Console.Error.WriteLine("REFERENCE_PROBE ETL SNAPSHOT_JSON WORKLOAD_START_UTC WORKLOAD_END_UTC OUTPUT_JSON"); return 2; }
var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
try
{
    var path = Path.GetFullPath(args[0]);
    if (!File.Exists(path) || new FileInfo(path).Length > 1024L*1024*1024) throw new ArgumentException("TraceMissingOrOverBudget");
    using var document = JsonDocument.Parse(File.ReadAllText(args[1]));
    var snapshot = document.RootElement.GetProperty("snapshot").Deserialize<DiagnosticSnapshot>(options) ?? throw new ArgumentException("SnapshotMissing");
    var workload = new MeasurementWindow(DateTimeOffset.Parse(args[2]), DateTimeOffset.Parse(args[3]));
    var started = System.Diagnostics.Stopwatch.StartNew();
    using var trace = TraceProcessor.Create(path, new TraceProcessorSettings { AllowLostEvents = true, AllowTimeInversion = false, SuppressFirstTimeSetupMessage = true });
    var metadata = trace.UseMetadata();
    var cpu = trace.UseCpuSamplingData();
    var scheduling = trace.UseCpuSchedulingData();
    var processes = trace.UseProcesses();
    var disks = trace.UseDiskIOData();
    trace.Process();
    if (metadata.ProcessorCount is < 1 or > 256) throw new InvalidOperationException("ProcessorCountNotQualified");
    var from = new[] { metadata.StartTime, workload.StartUtc, snapshot.StartedUtc }.Max();
    var to = new[] { metadata.StopTime, workload.EndUtc, snapshot.StartedUtc.AddSeconds(snapshot.DurationSeconds) }.Min();
    var wholeSeconds = Math.Max(0, (int)Math.Floor((to-from).TotalSeconds));
    var weights = new double[wholeSeconds];
    var counts = new int[wholeSeconds];
    var unknown = new int[wholeSeconds];
    var salt = RandomNumberGenerator.GetBytes(32);
    string? Identity(IProcess? p) => p?.CreateTime is { } time && SdkTimeConversion.TryTimestamp(time, DateTimeOffset.UnixEpoch, metadata.StopTime, out var created) ?
        Convert.ToHexString(HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(p.Id+":"+created.UtcTicks)))[..24] : null;
    var processCpu = new Dictionary<string, double>();
    long unlinkedCpu = 0;
    long invalidTimestamp = 0, partialTimestamp = 0, partialWeight = 0, invalidWeight = 0, missingProcess = 0;
    foreach (var sample in cpu.Result.Samples)
    {
        if (sample.Timestamp.HasValue && sample.Timestamp.IsPartial) partialTimestamp++;
        if (sample.Weight.HasValue && sample.Weight.IsPartial) partialWeight++;
        if (!SdkTimeConversion.TryTimestamp(sample.Timestamp, metadata.StartTime, metadata.StopTime, out var timestamp)) { invalidTimestamp++; continue; }
        var index = (int)Math.Floor((timestamp-from).TotalSeconds);
        if (index<0 || index>=weights.Length) continue;
        if (!SdkTimeConversion.TrySeconds(sample.Weight, out var weight)) { invalidWeight++; unknown[index]++; continue; }
        counts[index]++;
        if (sample.Process is null) { missingProcess++; unknown[index]++; continue; }
        if (sample.Process.Id == 0) continue;
        weights[index] += weight;
        var id=Identity(sample.Process);
        if (id is null) unlinkedCpu++; else processCpu[id]=processCpu.GetValueOrDefault(id)+weight;
    }
    var legacyReference = new IndependentTrace("Microsoft.TraceProcessing", true, metadata.LostEventCount, metadata.LostBufferCount,
        cpu.Result.Gaps.Count, new(from,to), weights.Select((w,i)=>new ReferencePoint(from.AddSeconds(i+1),counts[i]>=10 && unknown[i]==0 ? 100*w/metadata.ProcessorCount : null)).ToArray());
    var legacyQualification = ReferenceQualification.Evaluate(snapshot, legacyReference, workload);
    var schedulingIntervals = new List<CpuExecutionInterval>();
    long schedulingInvalidTimes = 0, schedulingUnknownThreads = 0;
    foreach (var slice in scheduling.Result.CpuTimeSlices)
    {
        if (!SdkTimeConversion.TryTimestamp(slice.StartTime, metadata.StartTime, metadata.StopTime, out var begin)
            || !SdkTimeConversion.TryTimestamp(slice.StopTime, begin, metadata.StopTime, out var end) || end <= begin)
        { schedulingInvalidTimes++; continue; }
        if (end <= from || begin >= to) continue;
        // Require both context-switch boundaries to agree on the scheduled thread.
        var known = slice.SwitchIn is not null && slice.SwitchOut is not null
            && slice.SwitchIn.ThreadId == slice.SwitchOut.ThreadId;
        if (!known) schedulingUnknownThreads++;
        var activity = !known ? CpuActivity.Unknown : slice.SwitchIn!.ThreadId == 0 ? CpuActivity.Idle : CpuActivity.Busy;
        if (schedulingIntervals.Count >= 4_000_000) throw new InvalidOperationException("SchedulingIntervalBudgetExceeded");
        schedulingIntervals.Add(new(slice.Processor, begin, end, activity));
    }
    var schedulingSummary = new MeasurementWindow(from, to).Valid
        ? CpuSchedulingReference.Normalize(schedulingIntervals, metadata.ProcessorCount, new(from, to)) : null;
    var sampledIdle = cpu.Result.Samples.Count(s => s.Thread?.Id == 0);
    var sampledMissingThread = cpu.Result.Samples.Count(s => s.Thread is null);
    // Retain old values for audit. Do not approve either estimator without independent agreement.
    var reference = new IndependentTrace("Microsoft.TraceProcessing", true, metadata.LostEventCount, metadata.LostBufferCount,
        cpu.Result.Gaps.Count, new(from, to), schedulingSummary?.Bins.Select(b => new ReferencePoint(b.EndUtc, b.CpuPercent)).ToArray() ?? []) { SemanticsQualified = false };
    var qualification = ReferenceQualification.Evaluate(snapshot, reference, workload);
    var io = disks.Result.Activity.Where(d=>SdkTimeConversion.TryTimestamp(d.CompleteTime, from, to, out var completed)
        && SdkTimeConversion.TryTimestamp(d.InitializeTime, from, completed, out _)).ToArray();
    var lifetimes = processes.Result.Processes.Where(p=>Identity(p) is not null).Select(p=>new { id=Identity(p),
        startedUtc=p.CreateTime!.Value.DateTimeOffset, endedUtc=p.ExitTime is { } end && SdkTimeConversion.TryTimestamp(end, p.CreateTime!.Value.DateTimeOffset, metadata.StopTime, out var ended) ? (DateTimeOffset?)ended : null }).ToArray();
    string traceHash;
    using (var hashInput = File.OpenRead(path)) traceHash = Convert.ToHexString(SHA256.HashData(hashInput));
    var report = new { status="PARTIAL", analyzer="Microsoft.Windows.EventTracing.Processing.All 1.12.10", etlSha256=traceHash,
        rawTracePublished=false, traceWindow=new { metadata.StartTime, metadata.StopTime },
        reference, qualification, legacyReference, legacyQualification, lifetimes, schedulingSummary,
        schedulingQualification = new { status="ReferenceIncomplete", reason="DPC/ISR time and WPA agreement are not independently qualified. Scheduling observations are not accuracy ground truth." },
        cpuParserDiagnostics = new { sampleCount=cpu.Result.Samples.Count, invalidTimestamp, partialTimestamp, partialWeight,
            invalidWeight, missingProcess, validSamplesInCommonWindow=counts.Sum(), unknownSamplesInCommonWindow=unknown.Sum(),
            estimatedUnaccountedSamplingPeriods=cpu.Result.Gaps.Count, schedulingTimeSliceCount=scheduling.Result.CpuTimeSlices.Count,
            sampledIdle, sampledMissingThread, schedulingInvalidTimes, schedulingUnknownThreads,
            interpretation="Weight is estimated time to the next sample. CpuGaps are estimated unaccounted sampling periods, not native lost events. Scheduling bins separately preserve idle, missing, unknown and overlapping time. The original sampled zero-gap gate remains in force." },
        processCpuSemantics="Legacy sampled weight estimate; not qualified running time or ground truth",
        processCpu=processCpu.OrderByDescending(p=>p.Value).Take(128).Select(p=>new { id=p.Key, cpuSeconds=p.Value, wholeMachinePercent=wholeSeconds>0?100*p.Value/(wholeSeconds*metadata.ProcessorCount):(double?)null }),
        unlinkedCpu, unlinkedIo=io.Count(d=>Identity(d.IssuingProcess) is null),
        disk=io.GroupBy(d=>d.Disk).Select(g=>new { sessionDiskNumber=g.Key, identityStatus="TraceLocalNumberOnly", operations=g.Count(),
            readBytes=g.Where(d=>d.IOType.ToString()=="Read").Sum(d=>d.Size.Bytes), writeBytes=g.Where(d=>d.IOType.ToString()=="Write").Sum(d=>d.Size.Bytes),
            latency=Statistics.Describe(g.Where(d=>d.IODuration.HasValue && d.IODuration.TotalMilliseconds >= 0).Select(d=>(double?)d.IODuration.TotalMilliseconds)) }),
        processIo=io.GroupBy(d=>Identity(d.IssuingProcess)).Select(g=>new { id=g.Key, operations=g.Count(), bytes=g.Sum(d=>d.Size.Bytes) }),
        resource = new { elapsedMilliseconds=started.Elapsed.TotalMilliseconds, peakWorkingSetBytes=System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64 },
        plans=RecoveryPlanner.Create(BottleneckAnalyzer.Analyze(snapshot)).Select(p=>new {plan=p, dryRun=RecoveryPlanner.DryRun(p)}) };
    File.WriteAllText(args[4],JsonSerializer.Serialize(report,options));
    CryptographicOperations.ZeroMemory(salt);
    Console.WriteLine($"REFERENCE_ANALYZED lostEvents={metadata.LostEventCount} lostBuffers={metadata.LostBufferCount} cpuGaps={cpu.Result.Gaps.Count} status={qualification.Status}");
    return 0;
}
catch(Exception e) { File.WriteAllText(args[4],JsonSerializer.Serialize(new {status="FAIL",error=e.GetType().Name, precision=(double?)null, recall=(double?)null},options)); Console.Error.WriteLine("REFERENCE_FAILED "+e.GetType().Name); return 3; }
