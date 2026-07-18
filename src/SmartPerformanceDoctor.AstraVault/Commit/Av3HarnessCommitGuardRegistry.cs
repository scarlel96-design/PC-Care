using System.Collections.Concurrent;

namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>
/// Per-canonical-root harness commit guard (E-9.1). In-flight is lease-scoped; duplicate tx ids are tracked per root until <see cref="PurgeRootHarnessState"/>.
/// </summary>
public sealed class Av3HarnessCommitGuardRegistry
{
    private readonly ConcurrentDictionary<string, RootGuardState> _roots = new(StringComparer.OrdinalIgnoreCase);
    private readonly AsyncLocal<int> _reentrancyDepth = new();

    public IAv3CommitGuardLease AcquireHarnessLease(string vaultRoot, Guid transactionId)
    {
        if (!Av3WriterAccessGate.TryNormalizeHarnessRoot(vaultRoot, out var canonicalRoot))
        {
            throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorIsolatedRootRequired);
        }

        if (_reentrancyDepth.Value > 0)
        {
            throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorReentrantCommit);
        }

        var state = _roots.GetOrAdd(canonicalRoot, _ => new RootGuardState());
        lock (state.Sync)
        {
            if (state.InFlight)
            {
                throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorCommitInFlight);
            }

            if (state.SeenTransactionIds.Contains(transactionId))
            {
                throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorDuplicateTransaction);
            }

            state.InFlight = true;
            state.SeenTransactionIds.Add(transactionId);
        }

        _reentrancyDepth.Value = _reentrancyDepth.Value + 1;
        return new HarnessLease(this, canonicalRoot, transactionId);
    }

    /// <summary>Removes all guard state for one harness root (after harness/dry-run completes).</summary>
    public void PurgeRootHarnessState(string vaultRoot)
    {
        if (Av3WriterAccessGate.TryNormalizeHarnessRoot(vaultRoot, out var canonicalRoot))
        {
            _roots.TryRemove(canonicalRoot, out _);
        }
    }

    internal bool IsRootInFlight(string vaultRoot)
    {
        if (!Av3WriterAccessGate.TryNormalizeHarnessRoot(vaultRoot, out var canonicalRoot))
        {
            return false;
        }

        return _roots.TryGetValue(canonicalRoot, out var state) && state.InFlight;
    }

    /// <summary>Diagnostic only — not used for production correctness (E-9.1).</summary>
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

        internal HashSet<Guid> SeenTransactionIds { get; } = [];
    }

    private sealed class HarnessLease : IAv3CommitGuardLease
    {
        private readonly Av3HarnessCommitGuardRegistry _registry;
        private int _disposed;

        internal HarnessLease(Av3HarnessCommitGuardRegistry registry, string canonicalRoot, Guid transactionId)
        {
            _registry = registry;
            CanonicalVaultRoot = canonicalRoot;
            TransactionId = transactionId;
        }

        public string CanonicalVaultRoot { get; }

        public Guid TransactionId { get; }

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