using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System.Diagnostics;

namespace SmartPerformanceDoctor.Diagnostics;

public sealed class EtwCollector : IAsyncDisposable
{
    private readonly EventBuffer _buffer;
    private readonly ProcessIdentityRegistry _identities;
    private readonly string _profile;
    private readonly DateTimeOffset _started;
    private TraceEventSession? _session;
    private Task? _worker, _consumer;
    private readonly CancellationTokenSource _consumeStop = new();
    private readonly object _gate = new();
    private readonly Dictionary<int, DiskAccumulator> _disks = [];
    private readonly Dictionary<string, ProcessAccumulator> _processes = [];
    private readonly Dictionary<string, long> _kinds = [];
    private readonly Dictionary<string, long> _receivedKinds = [];
    private readonly Dictionary<string, ProcessAccumulator> _totals = [];
    private long? _nativeLoss;
    private long _unattributed;
    private int _faults, _switches;
    private bool _disposed;
    private string _cleanup = "NotStarted", _lossStatus = "NotAvailable";
    public bool RangeReduced { get; private set; }
    public CollectionError? Error { get; private set; }
    public long Received => _buffer.Received;
    public long Lost => _buffer.QueueDropped + _buffer.Malformed + (_nativeLoss ?? 0);
    private sealed class DiskAccumulator
    { public long Read, Write; public int Ops; public readonly List<double> Latencies = []; }
    private sealed class ProcessAccumulator
    { public long Read, Write; public int Ops, Cpu; public double? First, Last; }
    public EtwCollector(int capacity, ProcessIdentityRegistry? identities = null, string profile = "General", DateTimeOffset? started = null)
    { _buffer = new(capacity); _identities = identities ?? new(); _profile = profile; _started = started ?? DateTimeOffset.UtcNow; }
    public static KernelTraceEventParser.Keywords Keywords(string profile) => profile switch
    {
        "Disabled" => KernelTraceEventParser.Keywords.None,
        "LowOverhead" => KernelTraceEventParser.Keywords.Process,
        "General" => KernelTraceEventParser.Keywords.DiskIO | KernelTraceEventParser.Keywords.Process | KernelTraceEventParser.Keywords.Thread,
        "Detailed" => KernelTraceEventParser.Keywords.DiskIO | KernelTraceEventParser.Keywords.MemoryHardFaults | KernelTraceEventParser.Keywords.Profile
            | KernelTraceEventParser.Keywords.Process | KernelTraceEventParser.Keywords.Thread | KernelTraceEventParser.Keywords.ContextSwitch,
        _ => throw new ArgumentException("UnknownProfile")
    };
    public void Start()
    {
        if (_disposed || _session is not null || _worker is not null) throw new InvalidOperationException("SessionAlreadyStarted");
        if (_profile == "Disabled") { _lossStatus = "DisabledByPolicy"; return; }
        if (TraceEventSession.IsElevated() != true) { Error = new("ETW", "PermissionDenied"); return; }
        var stage = "Create";
        try
        {
            _session = new TraceEventSession("PCCare.Diagnostics." + Guid.NewGuid().ToString("N"), TraceEventSessionOptions.NoRestartOnCreate)
                { StopOnDispose = true, BufferSizeMB = _profile == "LowOverhead" ? 2 : 8, BufferQuantumKB = 64 };
            stage = "EnableKernelProvider";
            _session.EnableKernelProvider(Keywords(_profile));
            stage = "Subscribe";
            void Offer(TraceEvent data, NumericEvent numeric)
            {
                var time = new DateTimeOffset(data.TimeStamp.ToUniversalTime());
                var id = _identities.Resolve(data.ProcessID, time);
                lock (_gate) _receivedKinds[numeric.Kind] = _receivedKinds.GetValueOrDefault(numeric.Kind) + 1;
                _buffer.Offer(numeric with { Identifier = id, Seconds = Math.Max(0, (time - _started).TotalSeconds) });
            }
            var source = _session.Source;
            source.Kernel.ProcessStart += d =>
            {
                var time = new DateTimeOffset(d.TimeStamp.ToUniversalTime());
                // A live API creation timestamp connects the event to the polling identity.
                // Refuse a process created after the event. PID reuse then stays unattributed.
                try
                {
                    using var p = Process.GetProcessById(d.ProcessID);
                    var birth = new DateTimeOffset(p.StartTime.ToUniversalTime());
                    if (birth <= time) _identities.ObserveSnapshot(d.ProcessID, birth, time);
                    else _identities.Start(d.ProcessID, time);
                }
                catch { _identities.Start(d.ProcessID, time); }
            };
            source.Kernel.ProcessStop += d => _identities.Stop(d.ProcessID, new(d.TimeStamp.ToUniversalTime()));
            source.Kernel.DiskIORead += d => Offer(d, new(d.DiskNumber, d.TransferSize, false, d.ElapsedTimeMSec, false, d.ProcessID));
            source.Kernel.DiskIOWrite += d => Offer(d, new(d.DiskNumber, d.TransferSize, true, d.ElapsedTimeMSec, false, d.ProcessID));
            source.Kernel.MemoryHardFault += d => Offer(d, new(0, 0, false, 0, true, d.ProcessID));
            source.Kernel.PerfInfoSample += d => Offer(d, new(0, 0, false, 0, false, d.ProcessID, CpuSample: true));
            source.Kernel.ThreadCSwitch += d => Offer(d, new(0, 0, false, 0, false, d.ProcessID, ContextSwitch: true));
            _cleanup = "Running";
            _worker = Task.Run(() => { try { source.Process(); } catch (Exception e) { Error = ErrorReporter.From("ETW.Process", e); } });
            _consumer = Task.Run(async () =>
            {
                try { while (!_consumeStop.IsCancellationRequested) { Aggregate(); await Task.Delay(20, _consumeStop.Token); } }
                catch (OperationCanceledException) { }
                finally { Aggregate(); }
            });
        }
        catch (Exception e)
        {
            Error = new("ETW." + stage, e.GetType().Name + ":0x" + e.HResult.ToString("X8"));
            try { _session?.Dispose(); _cleanup = "StoppedAfterStartFailure"; } catch { _cleanup = "Failed"; }
            _session = null;
        }
    }
    private void Aggregate()
    {
        var events = _buffer.Drain();
        lock (_gate)
        foreach (var e in events)
        {
            _kinds[e.Kind] = _kinds.GetValueOrDefault(e.Kind) + 1;
            if (e.Fault) { _faults++; continue; }
            if (e.ContextSwitch) { _switches++; continue; }
            if (!e.CpuSample)
            {
                if (!_disks.TryGetValue(e.Disk, out var d) && _disks.Count < 64) _disks[e.Disk] = d = new();
                if (d is not null)
                { if (e.Write) d.Write += e.Size; else d.Read += e.Size; d.Ops++; if (d.Latencies.Count < 256) d.Latencies.Add(e.Latency); else { var slot = Random.Shared.Next(d.Ops); if (slot < 256) d.Latencies[slot] = e.Latency; } }
            }
            if (e.Identifier is null) { _unattributed++; continue; }
            if (!_processes.TryGetValue(e.Identifier, out var p) && _processes.Count < 128) _processes[e.Identifier] = p = new();
            if (p is null) { _unattributed++; continue; }
            if (!_totals.TryGetValue(e.Identifier, out var total) && _totals.Count < 4096) _totals[e.Identifier] = total = new();
            if (total is not null) { total.First = total.First is null ? e.Seconds : Math.Min(total.First.Value, e.Seconds); total.Last = total.Last is null ? e.Seconds : Math.Max(total.Last.Value, e.Seconds); if (e.CpuSample) total.Cpu++; else { if (e.Write) total.Write += e.Size; else total.Read += e.Size; total.Ops++; } }
            p.First = p.First is null ? e.Seconds : Math.Min(p.First.Value, e.Seconds); p.Last = p.Last is null ? e.Seconds : Math.Max(p.Last.Value, e.Seconds);
            if (e.CpuSample) p.Cpu++;
            else { if (e.Write) p.Write += e.Size; else p.Read += e.Size; p.Ops++; }
        }
    }
    public (IReadOnlyList<DiskMetric> Disks, int Faults, IReadOnlyList<EtwProcessMetric> Processes, int Switches) Drain()
    {
        lock (_gate)
        {
            var result = (_disks.Select(g => new DiskMetric(g.Key, g.Value.Read, g.Value.Write, g.Value.Ops,
                Statistics.Describe(g.Value.Latencies.Select(x => (double?)x)).P95) { LatencySampleCount = g.Value.Latencies.Count }).ToArray(), _faults,
                _processes.Select(g => new EtwProcessMetric(g.Key, g.Value.Cpu, g.Value.Read, g.Value.Write, g.Value.Ops) { ObservedStartSeconds = g.Value.First, ObservedEndSeconds = g.Value.Last }).ToArray(), _switches);
            _disks.Clear(); _processes.Clear(); _faults = 0; _switches = 0; return result;
        }
    }
    public IReadOnlyList<EtwProcessMetric> ExportContributions()
    { lock (_gate) return _totals.Select(g => new EtwProcessMetric(g.Key, g.Value.Cpu, g.Value.Read, g.Value.Write, g.Value.Ops) { ObservedStartSeconds = g.Value.First, ObservedEndSeconds = g.Value.Last }).ToArray(); }
    public EtwStatistics GetStatistics()
    {
        lock (_gate)
        {
            var mb = _profile == "LowOverhead" ? 2 : 8;
            var quantum = Math.Max(64, Environment.ProcessorCount);
            var minimum = mb * 1024 / quantum;
            return new(_nativeLoss, _buffer.QueueDropped, _buffer.Malformed, _buffer.Accepted, _buffer.Processed, _buffer.Pending,
                _cleanup, _lossStatus, RangeReduced ? "DisabledAfterLoss" : _profile, RangeReduced, _unattributed,
                _buffer.MaxDelayMilliseconds, mb, quantum, minimum, minimum * 5 / 4 + 10, new Dictionary<string, long>(_receivedKinds));
        }
    }
    public async Task ReduceIfOverloadedAsync()
    {
        if (_session is null) return;
        try { _nativeLoss = _session.EventsLost; _lossStatus = "SessionCounterQueried;TerminalIntervalUnknown"; }
        catch { _lossStatus = "QueryFailed"; }
        if (_buffer.QueueDropped > 0 || _nativeLoss > 0) { RangeReduced = true; await DisposeAsync(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (_session is not null)
        {
            var name = _session.SessionName;
            try { _nativeLoss = _session.EventsLost; _lossStatus = "SessionCounterBeforeStop;TerminalIntervalUnknown"; }
            catch { _lossStatus = "QueryFailed"; }
            try { _session.Dispose(); _cleanup = "Stopped"; } catch (Exception e) { _cleanup = "Failed"; Error = ErrorReporter.From("ETW.Stop", e); }
            if (_worker is not null)
                try { await _worker.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception e) { _cleanup = "Failed"; Error = ErrorReporter.From("ETW.Stop", e); }
            try { if (_cleanup != "Failed") _cleanup = TraceEventSession.GetActiveSessionNames().Contains(name) ? "Failed" : "VerifiedAbsent"; }
            catch { if (_cleanup != "Failed") _cleanup = "StopRequestedUnverified"; }
            _session = null;
        }
        _consumeStop.Cancel();
        if (_consumer is not null) await _consumer.WaitAsync(TimeSpan.FromSeconds(5));
        _consumeStop.Dispose();
    }
}
