using System.Collections.Concurrent;
using SmartPerformanceDoctor.AstraVault.Commit;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Per-canonical-root harness anchor update guard (E-11).</summary>
public sealed class Av3AnchorGuardRegistry
{
    private readonly ConcurrentDictionary<string, RootGuardState> _roots = new(StringComparer.OrdinalIgnoreCase);
    private readonly AsyncLocal<int> _reentrancyDepth = new();

    public IAv3AnchorGuardLease AcquireHarnessLease(string vaultRoot, Guid updateId)
    {
        if (!Av3WriterAccessGate.TryNormalizeHarnessRoot(vaultRoot, out var canonicalRoot))
        {
            throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorIsolatedRootRequired);
        }

        if (_reentrancyDepth.Value > 0)
        {
            throw new Av3WriterRouteBlockedException(Av3AnchorRuntimePolicy.ErrorReentrantAnchorUpdate);
        }

        var state = _roots.GetOrAdd(canonicalRoot, _ => new RootGuardState());
        lock (state.Sync)
        {
            if (state.InFlight)
            {
                throw new Av3WriterRouteBlockedException(Av3AnchorRuntimePolicy.ErrorAnchorUpdateInFlight);
            }

            if (state.SeenUpdateIds.Contains(updateId))
            {
                throw new Av3WriterRouteBlockedException(Av3AnchorRuntimePolicy.ErrorDuplicateAnchorUpdate);
            }

            state.InFlight = true;
            state.SeenUpdateIds.Add(updateId);
        }

        _reentrancyDepth.Value = _reentrancyDepth.Value + 1;
        return new HarnessLease(this, canonicalRoot, updateId);
    }

    public void PurgeRootHarnessState(string vaultRoot)
    {
        if (Av3WriterAccessGate.TryNormalizeHarnessRoot(vaultRoot, out var canonicalRoot))
        {
            _roots.TryRemove(canonicalRoot, out _);
        }
    }

    internal void DiagnosticResetAllHarnessStateForTests()
    {
        _roots.Clear();
        _reentrancyDepth.Value = 0;
    }

    private void ReleaseLease(string canonicalRoot)
    {
        if (_roots.TryGetValue(canonicalRoot, out var state))
        {
            lock (state.Sync)
            {
                state.InFlight = false;
            }
        }

        _reentrancyDepth.Value = Math.Max(0, _reentrancyDepth.Value - 1);
    }

    private sealed class RootGuardState
    {
        internal object Sync { get; } = new();

        internal bool InFlight { get; set; }

        internal HashSet<Guid> SeenUpdateIds { get; } = [];
    }

    private sealed class HarnessLease : IAv3AnchorGuardLease
    {
        private readonly Av3AnchorGuardRegistry _registry;
        private int _disposed;

        internal HarnessLease(Av3AnchorGuardRegistry registry, string canonicalRoot, Guid updateId)
        {
            _registry = registry;
            CanonicalVaultRoot = canonicalRoot;
            UpdateId = updateId;
        }

        public string CanonicalVaultRoot { get; }

        public Guid UpdateId { get; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _registry.ReleaseLease(CanonicalVaultRoot);
        }
    }
}