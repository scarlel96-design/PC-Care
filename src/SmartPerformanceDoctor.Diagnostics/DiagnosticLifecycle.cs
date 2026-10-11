namespace SmartPerformanceDoctor.Diagnostics;

public sealed record DiagnosticLease(int Generation, CancellationToken Token);
/// <summary>Keep one collector active until its cleanup finishes.</summary>
public sealed class DiagnosticLifecycle
{
    private CancellationTokenSource? _source;
    private int _generation;
    private int _active;
    public bool TryBegin(out DiagnosticLease? lease)
    {
        lease = null;
        if (_source is not null) return false;
        _source = new(); _active = ++_generation;
        lease = new(_active, _source.Token);
        return true;
    }
    public bool IsCurrent(DiagnosticLease lease) => _source is not null && lease.Generation == _generation;
    public void Cancel(bool invalidate = false) { if (invalidate) _generation++; _source?.Cancel(); }
    public bool Complete(DiagnosticLease lease)
    {
        if (_source is null || _active != lease.Generation) return false;
        _source.Dispose(); _source = null;
        return true;
    }
}
