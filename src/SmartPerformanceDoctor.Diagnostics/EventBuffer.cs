using System.Threading.Channels;

namespace SmartPerformanceDoctor.Diagnostics;

public sealed record NumericEvent(int Disk, int Size, bool Write, double Latency, bool Fault, int ProcessId = -1, bool CpuSample = false, bool ContextSwitch = false);
public sealed class EventBuffer
{
    private readonly Channel<NumericEvent> _queue;
    private long _received, _dropped;
    private readonly int _capacity;
    public long Received => Interlocked.Read(ref _received);
    public long Dropped => Interlocked.Read(ref _dropped);
    public EventBuffer(int capacity)
    {
        if (capacity is < 32 or > 4096) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _queue = Channel.CreateBounded<NumericEvent>(new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });
    }
    public void Offer(NumericEvent value)
    {
        Interlocked.Increment(ref _received);
        if (value.Size < 0 || !double.IsFinite(value.Latency) || value.Latency < 0 || !_queue.Writer.TryWrite(value)) Interlocked.Increment(ref _dropped);
    }
    public IReadOnlyList<NumericEvent> Drain()
    {
        var result = new List<NumericEvent>();
        while (result.Count < _capacity && _queue.Reader.TryRead(out var value)) result.Add(value);
        return result;
    }
}
public static class ErrorReporter
{
    public static CollectionError From(string provider, Exception error) => new(provider, error is UnauthorizedAccessException ? "PermissionDenied" : error.GetType().Name);
}
