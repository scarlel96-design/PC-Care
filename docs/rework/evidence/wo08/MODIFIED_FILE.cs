using System.Threading.Channels;
using System.Diagnostics;

namespace SmartPerformanceDoctor.Diagnostics;

public sealed record NumericEvent(int Disk, int Size, bool Write, double Latency, bool Fault, int ProcessId = -1, bool CpuSample = false, bool ContextSwitch = false, string? Identifier = null, double Seconds = 0)
{
    internal long QueuedAt { get; init; }
    public string Kind => Fault ? "HardFault" : CpuSample ? "CpuSample" : ContextSwitch ? "ContextSwitch" : Write ? "DiskWrite" : "DiskRead";
}
public sealed class EventBuffer
{
    private readonly Channel<NumericEvent> _queue;
    private long _received, _dropped, _malformed, _accepted, _processed, _maxDelayTicks;
    private readonly int _capacity;
    public long Received => Interlocked.Read(ref _received);
    public long Dropped => QueueDropped + Malformed;
    public long QueueDropped => Interlocked.Read(ref _dropped);
    public long Malformed => Interlocked.Read(ref _malformed);
    public long Accepted => Interlocked.Read(ref _accepted);
    public long Processed => Interlocked.Read(ref _processed);
    public long Pending => Math.Max(0, Accepted - Processed);
    public double MaxDelayMilliseconds => 1000d * Interlocked.Read(ref _maxDelayTicks) / Stopwatch.Frequency;
    public EventBuffer(int capacity)
    {
        if (capacity is < 32 or > 4096) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _queue = Channel.CreateBounded<NumericEvent>(new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    }
    public void Offer(NumericEvent value)
    {
        Interlocked.Increment(ref _received);
        if (value.Size < 0 || !double.IsFinite(value.Latency) || value.Latency < 0 || !double.IsFinite(value.Seconds) || value.Seconds < 0)
        { Interlocked.Increment(ref _malformed); return; }
        if (_queue.Writer.TryWrite(value with { QueuedAt = Stopwatch.GetTimestamp() })) Interlocked.Increment(ref _accepted);
        else Interlocked.Increment(ref _dropped);
    }
    public IReadOnlyList<NumericEvent> Drain()
    {
        var result = new List<NumericEvent>();
        while (result.Count < _capacity && _queue.Reader.TryRead(out var value))
        {
            result.Add(value); Interlocked.Increment(ref _processed);
            var delay = Stopwatch.GetTimestamp() - value.QueuedAt;
            long old;
            do { old = Interlocked.Read(ref _maxDelayTicks); if (delay <= old) break; }
            while (Interlocked.CompareExchange(ref _maxDelayTicks, delay, old) != old);
        }
        return result;
    }
}
public static class ErrorReporter
{
    public static CollectionError From(string provider, Exception error) => new(provider, error is UnauthorizedAccessException ? "PermissionDenied" : error.GetType().Name);
}
