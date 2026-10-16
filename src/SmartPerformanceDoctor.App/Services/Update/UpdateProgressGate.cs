namespace SmartPerformanceDoctor.App.Services.Update;

/// <summary>Invalidate queued reports when an operation ends or a new operation starts.</summary>
public sealed class UpdateProgressGate
{
    private long _epoch;
    public long Capture() => Interlocked.Read(ref _epoch);
    public void Invalidate() => Interlocked.Increment(ref _epoch);
    public bool IsCurrent(long epoch) => Capture() == epoch;
}
