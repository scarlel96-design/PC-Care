using System.Security.Cryptography;
using System.Text;

namespace SmartPerformanceDoctor.Diagnostics;

/// <summary>Keep raw process identifiers in memory. Export session-local lifetime identifiers only.</summary>
public sealed class ProcessIdentityRegistry
{
    private sealed record Entry(int Pid, DateTimeOffset Start, DateTimeOffset? End, string Id, string Source);
    private readonly List<Entry> _entries = [];
    private readonly object _gate = new();
    private readonly string _salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public bool Truncated { get; private set; }
    public string? ObserveSnapshot(int pid, DateTimeOffset start, DateTimeOffset observed)
    {
        if (pid < 0 || start > observed) return null;
        lock (_gate)
        {
            var exact = _entries.LastOrDefault(x => x.Pid == pid && x.Start == start && (x.End is null || observed < x.End));
            if (exact is not null) return exact.Id;
            foreach (var old in _entries.Where(x => x.Pid == pid && x.End is null).ToArray())
                _entries[_entries.IndexOf(old)] = old with { End = start >= old.Start ? start : observed };
            return Add(pid, start, "ProcessApiStartTime");
        }
    }
    public string? Start(int pid, DateTimeOffset time)
    {
        if (pid < 0) return null;
        lock (_gate)
        {
            var live = _entries.LastOrDefault(x => x.Pid == pid && x.End is null);
            if (live is not null && time < live.Start) return null;
            if (live is not null) _entries[_entries.IndexOf(live)] = live with { End = time };
            return Add(pid, time, "EtwStart");
        }
    }
    public void Stop(int pid, DateTimeOffset time)
    {
        lock (_gate)
        {
            var entry = _entries.LastOrDefault(x => x.Pid == pid && x.Start <= time && x.End is null);
            if (entry is not null) _entries[_entries.IndexOf(entry)] = entry with { End = time };
        }
    }
    public string? Resolve(int pid, DateTimeOffset time)
    {
        lock (_gate)
        {
            var found = _entries.Where(x => x.Pid == pid && x.Start <= time && (x.End is null || time < x.End)).ToArray();
            return found.Length == 1 ? found[0].Id : null;
        }
    }
    public IReadOnlyList<ProcessLifetime> Export()
    { lock (_gate) return _entries.Select(x => new ProcessLifetime(x.Id, x.Start, x.End, x.Source)).ToArray(); }
    private string? Add(int pid, DateTimeOffset start, string source)
    {
        if (_entries.Count >= 4096) { Truncated = true; return null; }
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{_salt}:{pid}:{start.UtcTicks}:{_entries.Count}")))[..24];
        _entries.Add(new(pid, start, null, id, source)); return id;
    }
}
public static class ProcessSelection
{
    public static IReadOnlyList<ProcessMetric> Select(IEnumerable<ProcessMetric> candidates, int budget, IReadOnlyList<EtwProcessMetric>? io = null)
    {
        if (budget is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(budget));
        var all = candidates.Where(p => p.WorkingSetBytes >= 0).ToArray();
        var cpu = all.OrderByDescending(p => p.CpuPercent ?? -1).ThenBy(p => p.Identifier, StringComparer.Ordinal);
        var memory = all.OrderByDescending(p => p.WorkingSetBytes).ThenBy(p => p.Identifier, StringComparer.Ordinal);
        var ioIds = (io ?? []).Where(p => p.Operations > 0 && p.ReadBytes >= 0 && p.WriteBytes >= 0)
            .OrderByDescending(p => (decimal)p.ReadBytes + p.WriteBytes).Select(p => p.Identifier).Distinct().Take(Math.Max(1, budget / 3)).ToArray();
        var ioCandidates = ioIds.Select(id => all.FirstOrDefault(p => p.Identifier == id)).OfType<ProcessMetric>().ToArray();
        var remaining = budget - ioCandidates.Length;
        return ioCandidates.Concat(cpu.Take((remaining + 1) / 2)).Concat(memory.Take(remaining / 2)).Concat(cpu).DistinctBy(p => p.Identifier).Take(budget).ToArray();
    }
}
