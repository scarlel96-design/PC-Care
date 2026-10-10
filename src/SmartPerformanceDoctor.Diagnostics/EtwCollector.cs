using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;

namespace SmartPerformanceDoctor.Diagnostics;

public sealed class EtwCollector : IAsyncDisposable
{
    private readonly EventBuffer _buffer;
    private TraceEventSession? _session;
    private Task? _worker;
    private readonly string _salt = Guid.NewGuid().ToString("N");
    private long _finalNativeLoss;
    public CollectionError? Error { get; private set; }
    public long Received => _buffer.Received;
    public long Lost => _buffer.Dropped + _finalNativeLoss;
    public EtwCollector(int capacity) => _buffer = new EventBuffer(capacity);
    public void Start()
    {
        if (_session is not null || _worker is not null) throw new InvalidOperationException("SessionAlreadyStarted");
        if (TraceEventSession.IsElevated() != true) { Error = new("ETW", "PermissionDenied"); return; }
        try
        {
            // Never attach to, restart, or stop a session owned by another collector.
            _session = new TraceEventSession("PCCare.Diagnostics." + Guid.NewGuid().ToString("N"), TraceEventSessionOptions.NoRestartOnCreate)
                { StopOnDispose = true, BufferSizeMB = 8 };
            void Disk(DiskIOTraceData data, bool write) => _buffer.Offer(new(data.DiskNumber, data.TransferSize, write, data.ElapsedTimeMSec, false, data.ProcessID));
            _session.Source.Kernel.DiskIORead += d => Disk(d, false);
            _session.Source.Kernel.DiskIOWrite += d => Disk(d, true);
            _session.Source.Kernel.MemoryHardFault += _ => _buffer.Offer(new(0, 0, false, 0, true));
            _session.Source.Kernel.PerfInfoSample += d => _buffer.Offer(new(0, 0, false, 0, false, d.ProcessID, CpuSample: true));
            _session.Source.Kernel.ThreadCSwitch += d => _buffer.Offer(new(0, 0, false, 0, false, d.ProcessID, ContextSwitch: true));
            _session.EnableKernelProvider(KernelTraceEventParser.Keywords.DiskIO | KernelTraceEventParser.Keywords.MemoryHardFaults
                | KernelTraceEventParser.Keywords.Profile | KernelTraceEventParser.Keywords.Process | KernelTraceEventParser.Keywords.Thread | KernelTraceEventParser.Keywords.ContextSwitch);
            var source = _session.Source;
            _worker = Task.Run(() => { try { source.Process(); } catch (Exception e) { Error = new("ETW", e.GetType().Name); } });
        }
        catch (Exception e) { Error = new("ETW", e.GetType().Name); _session?.Dispose(); _session = null; }
    }
    public (IReadOnlyList<DiskMetric> Disks, int Faults, IReadOnlyList<EtwProcessMetric> Processes, int Switches) Drain()
    {
        var events = _buffer.Drain();
        string Mask(int pid) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(_salt + pid)))[..12];
        return (events.Where(e => !e.Fault && !e.CpuSample && !e.ContextSwitch).GroupBy(e => e.Disk).Take(64).Select(g => new DiskMetric(g.Key,
            g.Where(e => !e.Write).Sum(e => (long)e.Size), g.Where(e => e.Write).Sum(e => (long)e.Size), g.Count(),
            Statistics.Describe(g.Select(e => (double?)e.Latency)).P95)).ToArray(), events.Count(e => e.Fault),
            events.Where(e => e.ProcessId >= 0 && !e.Fault && !e.ContextSwitch).GroupBy(e => e.ProcessId).Take(128)
                .Select(g => new EtwProcessMetric(Mask(g.Key), g.Count(e => e.CpuSample), g.Where(e => !e.Write).Sum(e => (long)e.Size),
                    g.Where(e => e.Write).Sum(e => (long)e.Size), g.Count(e => !e.CpuSample))).ToArray(), events.Count(e => e.ContextSwitch));
    }
    public async ValueTask DisposeAsync()
    {
        if (_session is null) return;
        try { _finalNativeLoss = _session.EventsLost; }
        catch (Exception e) { Error = ErrorReporter.From("ETW.Loss", e); }
        try { _session.Dispose(); }
        catch (Exception e) { Error = ErrorReporter.From("ETW.Stop", e); }
        if (_worker is not null)
            try { await _worker.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (Exception e) { Error = ErrorReporter.From("ETW.Stop", e); }
        _session = null; _worker = null;
    }
}
