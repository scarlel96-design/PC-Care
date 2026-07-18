using System.Text.Json;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-11 file-based harness rollback anchor (public digests only).</summary>
public sealed class Av3HarnessRollbackAnchor : IAv3RollbackAnchor
{
    private static readonly Av3AnchorGuardRegistry SharedGuard = new();

    public Task<Av3AnchorSnapshot?> ReadAnchorAsync(string vaultRoot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureHarnessRoute(vaultRoot);
        return Task.FromResult(ReadStateOrNull(vaultRoot));
    }

    public Task<Av3AnchorVerificationResult> VerifyAnchorAsync(
        string vaultRoot,
        ulong observedGeneration,
        ReadOnlyMemory<byte> witnessDigest,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureHarnessRoute(vaultRoot);
        var snapshot = ReadStateOrNull(vaultRoot);
        return Task.FromResult(Av3AnchorClassifier.BuildVerification(snapshot, observedGeneration, witnessDigest.Span));
    }

    public Task<Av3AnchorUpdateResult> PrepareAnchorUpdateAsync(
        Av3AnchorUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureHarnessRoute(request.VaultRoot, request.TestHarnessInvocation);

        if (request.WitnessDigest.Length == 0)
        {
            return Task.FromResult(Fail(Av3AnchorFailureReason.StateCorrupt, "av3_anchor_witness_missing"));
        }

        var current = ReadStateOrNull(request.VaultRoot);
        var nextCounter = (current?.MonotonicCounter ?? 0UL) + 1UL;
        if (current is not null && request.TargetGeneration < current.Generation)
        {
            return Task.FromResult(Fail(Av3AnchorFailureReason.MonotonicityViolation, "av3_anchor_monotonicity_violation"));
        }

        var pending = new PendingDocument
        {
            Schema = 1,
            UpdateId = request.UpdateId,
            ContainerId = request.ContainerId,
            TargetGeneration = request.TargetGeneration,
            WitnessDigestHex = Convert.ToHexString(request.WitnessDigest.Span),
            MonotonicCounter = nextCounter,
            ProviderKind = Av3AnchorProviderKind.HarnessFileMonotonic.ToString()
        };

        try
        {
            WritePending(request.VaultRoot, pending);
        }
        catch
        {
            return Task.FromResult(Fail(Av3AnchorFailureReason.StateCorrupt, "av3_anchor_pending_write_failed"));
        }

        return Task.FromResult(new Av3AnchorUpdateResult
        {
            Success = true,
            FailureReason = Av3AnchorFailureReason.None,
            PublicErrorClass = "ok",
            Snapshot = new Av3AnchorSnapshot
            {
                ContainerId = request.ContainerId,
                Generation = request.TargetGeneration,
                MonotonicCounter = nextCounter,
                WitnessDigestHex = pending.WitnessDigestHex,
                ProviderKind = Av3AnchorProviderKind.HarnessFileMonotonic
            }
        });
    }

    public async Task<Av3AnchorUpdateResult> CommitAnchorUpdateAsync(
        string vaultRoot,
        Guid updateId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureHarnessRoute(vaultRoot);

        using var lease = SharedGuard.AcquireHarnessLease(vaultRoot, updateId);
        if (TestingHoldCommitMilliseconds > 0)
        {
            await Task.Delay(TestingHoldCommitMilliseconds, cancellationToken).ConfigureAwait(false);
        }

        var pending = ReadPendingOrNull(vaultRoot);
        if (pending is null || pending.UpdateId != updateId)
        {
            return Fail(Av3AnchorFailureReason.PendingUpdateMissing, "av3_anchor_pending_missing");
        }

        var current = ReadStateOrNull(vaultRoot);
        if (current is not null && pending.TargetGeneration < current.Generation)
        {
            return Fail(Av3AnchorFailureReason.MonotonicityViolation, "av3_anchor_monotonicity_violation");
        }

        var state = new StateDocument
        {
            Schema = 1,
            ContainerId = pending.ContainerId,
            Generation = pending.TargetGeneration,
            MonotonicCounter = pending.MonotonicCounter,
            WitnessDigestHex = pending.WitnessDigestHex,
            ProviderKind = pending.ProviderKind
        };

        try
        {
            await WriteStateAsync(vaultRoot, state, cancellationToken).ConfigureAwait(false);
            DeletePending(vaultRoot);
        }
        catch
        {
            return Fail(Av3AnchorFailureReason.StateCorrupt, "av3_anchor_state_write_failed");
        }

        return new Av3AnchorUpdateResult
        {
            Success = true,
            FailureReason = Av3AnchorFailureReason.None,
            PublicErrorClass = "ok",
            Snapshot = ToSnapshot(state)
        };
    }

    public Task AbortAnchorUpdateAsync(
        string vaultRoot,
        Guid updateId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureHarnessRoute(vaultRoot);

        var pending = ReadPendingOrNull(vaultRoot);
        if (pending is not null && pending.UpdateId == updateId)
        {
            DeletePending(vaultRoot);
        }

        return Task.CompletedTask;
    }

    public Av3AnchorStatus ClassifyAnchorFailure(Av3AnchorFailureReason reason) =>
        Av3AnchorClassifier.ClassifyFailure(reason);

    /// <summary>Test-only: extends commit hold time to exercise concurrent guard (E-11).</summary>
    internal static int TestingHoldCommitMilliseconds;

    internal static void ClearHarnessState(string vaultRoot) =>
        SharedGuard.PurgeRootHarnessState(vaultRoot);

    private static void EnsureHarnessRoute(string vaultRoot, bool testHarnessInvocation = true)
    {
        if (!Av3PhaseGate.ProductionAnchorImplemented)
        {
            if (!testHarnessInvocation || !Av3AnchorRuntimePolicy.HarnessAnchorEnabled)
            {
                throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorAnchorProductionDisabled);
            }
        }

        Av3AnchorHarnessScope.Ensure(vaultRoot, testHarnessInvocation);
    }

    private static Av3AnchorSnapshot? ReadStateOrNull(string vaultRoot)
    {
        var path = StatePath(vaultRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<StateDocument>(json);
            return doc is null ? null : ToSnapshot(doc);
        }
        catch
        {
            return null;
        }
    }

    private static PendingDocument? ReadPendingOrNull(string vaultRoot)
    {
        var path = PendingPath(vaultRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PendingDocument>(json);
        }
        catch
        {
            return null;
        }
    }

    private static void WritePending(string vaultRoot, PendingDocument pending)
    {
        var dir = Av3AnchorHarnessScope.ResolveAnchorStoreDirectory(vaultRoot);
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(pending);
        File.WriteAllText(PendingPath(vaultRoot), json);
    }

    private static async Task WriteStateAsync(string vaultRoot, StateDocument state, CancellationToken cancellationToken)
    {
        var dir = Av3AnchorHarnessScope.ResolveAnchorStoreDirectory(vaultRoot);
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(state);
        await File.WriteAllTextAsync(StatePath(vaultRoot), json, cancellationToken).ConfigureAwait(false);
    }

    private static void DeletePending(string vaultRoot)
    {
        var path = PendingPath(vaultRoot);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string StatePath(string vaultRoot) =>
        Path.Combine(
            Av3AnchorHarnessScope.ResolveAnchorStoreDirectory(vaultRoot),
            Av3AnchorRuntimePolicy.StateFileName);

    private static string PendingPath(string vaultRoot) =>
        Path.Combine(
            Av3AnchorHarnessScope.ResolveAnchorStoreDirectory(vaultRoot),
            Av3AnchorRuntimePolicy.PendingFileName);

    private static Av3AnchorSnapshot ToSnapshot(StateDocument doc)
    {
        Enum.TryParse<Av3AnchorProviderKind>(doc.ProviderKind, out var providerKind);
        return new Av3AnchorSnapshot
        {
            ContainerId = doc.ContainerId,
            Generation = doc.Generation,
            MonotonicCounter = doc.MonotonicCounter,
            WitnessDigestHex = doc.WitnessDigestHex,
            ProviderKind = providerKind == default ? Av3AnchorProviderKind.HarnessFileMonotonic : providerKind
        };
    }

    private static Av3AnchorUpdateResult Fail(Av3AnchorFailureReason reason, string publicErrorClass) =>
        new()
        {
            Success = false,
            FailureReason = reason,
            PublicErrorClass = publicErrorClass
        };

    private sealed class StateDocument
    {
        public int Schema { get; set; }

        public Guid ContainerId { get; set; }

        public ulong Generation { get; set; }

        public ulong MonotonicCounter { get; set; }

        public string WitnessDigestHex { get; set; } = string.Empty;

        public string ProviderKind { get; set; } = Av3AnchorProviderKind.HarnessFileMonotonic.ToString();
    }

    private sealed class PendingDocument
    {
        public int Schema { get; set; }

        public Guid UpdateId { get; set; }

        public Guid ContainerId { get; set; }

        public ulong TargetGeneration { get; set; }

        public string WitnessDigestHex { get; set; } = string.Empty;

        public ulong MonotonicCounter { get; set; }

        public string ProviderKind { get; set; } = Av3AnchorProviderKind.HarnessFileMonotonic.ToString();
    }
}