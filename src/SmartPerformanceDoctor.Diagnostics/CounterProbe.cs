using Windows.Win32;
using Windows.Win32.System.Performance;

namespace SmartPerformanceDoctor.Diagnostics;

public sealed class CounterProbe : IDisposable
{
    private readonly PdhCloseQuerySafeHandle _query;
    private readonly Dictionary<string, PDH_HCOUNTER> _counters = new();
    public CounterProbe(IEnumerable<KeyValuePair<string, string>> paths)
    {
        if (PInvoke.PdhOpenQuery(null, 0, out _query) != 0) throw new IOException("PDH query unavailable");
        foreach (var (name, path) in paths)
            if (PInvoke.PdhAddEnglishCounter(_query, path, 0, out var counter) == 0) _counters[name] = counter;
        PInvoke.PdhCollectQueryData((PDH_HQUERY)_query.DangerousGetHandle());
    }
    public IReadOnlyDictionary<string, double?> Read()
    {
        var result = new Dictionary<string, double?>();
        var collected = PInvoke.PdhCollectQueryData((PDH_HQUERY)_query.DangerousGetHandle()) == 0;
        foreach (var (name, counter) in _counters)
        {
            var status = PInvoke.PdhGetFormattedCounterValue(counter, PDH_FMT.PDH_FMT_DOUBLE, out _, out var value);
            result[name] = collected && status == 0 && value.CStatus <= 1 && double.IsFinite(value.Anonymous.doubleValue)
                ? value.Anonymous.doubleValue : null;
        }
        return result;
    }
    public void Dispose() => _query.Dispose();
}
