using System.Diagnostics;

namespace SmartPerformanceDoctor.UpdateEngine;

public sealed record UpdatePhaseMeasurement(string Name, long Calls, double ElapsedMs, long AllocatedBytes);

/// <summary>Record opt-in elapsed times. Nested measurements overlap and must not be added.</summary>
public sealed class UpdatePerformanceProfile
{
    private static readonly AsyncLocal<UpdatePerformanceProfile?> Current = new();
    internal static UpdatePerformanceProfile? Active => Current.Value;
    internal IDisposable Activate() => new Activation(this);
    private readonly Dictionary<string,(long Calls,long Ticks,long Allocated)> _values = [];
    public IReadOnlyList<UpdatePhaseMeasurement> Snapshot() => _values.Select(p =>
        new UpdatePhaseMeasurement(p.Key,p.Value.Calls,p.Value.Ticks*1000.0/Stopwatch.Frequency,p.Value.Allocated)).ToArray();
    internal IDisposable Measure(string name) => new Scope(this,name);
    private sealed class Activation : IDisposable
    {
        private readonly UpdatePerformanceProfile? _previous=Current.Value;
        public Activation(UpdatePerformanceProfile profile) { Current.Value=profile; }
        public void Dispose() { Current.Value=_previous; }
    }
    private sealed class Scope(UpdatePerformanceProfile owner,string name) : IDisposable
    {
        private readonly long _start=Stopwatch.GetTimestamp();
        private readonly long _allocated=GC.GetAllocatedBytesForCurrentThread();
        public void Dispose()
        {
            var previous=owner._values.GetValueOrDefault(name);
            owner._values[name]=(previous.Calls+1,previous.Ticks+Stopwatch.GetTimestamp()-_start,previous.Allocated+GC.GetAllocatedBytesForCurrentThread()-_allocated);
        }
    }
}
