using System.Text.Json;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-13 harness trusted anchor (av3-e13- temp; deterministic monotonic counter).</summary>
public sealed class Av3HarnessTrustedAnchorProvider : IAv3TrustedAnchorProvider
{
    private static readonly Av3AnchorGuardRegistry SharedGuard = new();

    public Av3TrustedAnchorProviderKind ProviderKind => Av3TrustedAnchorProviderKind.HarnessSynthetic;

    public bool IsAvailableForProductionEnable => false;

    public Task<Av3TrustedAnchorWitness?> ReadWitnessAsync(string vaultRoot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureHarnessRoute(vaultRoot);
        return Task.FromResult(ReadStateOrNull(vaultRoot));
    }

    public Task<Av3TrustedAnchorVerification> VerifyWitnessAsync(
        Av3TrustedAnchorRequest context,
        ulong observedVaultGeneration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureHarnessRoute(context.VaultRoot, context.TestHarnessInvocation);
        var witness = ReadStateOrNull(context.VaultRoot);
        if (witness is null)
        {
            return Task.FromResult(new Av3TrustedAnchorVerification
            {
                Verified = false,
                AnchorStatus = Av3AnchorStatus.AnchorUnavailable,
                FailureReason = Av3TrustedAnchorFailureReason.ProviderUnavailable,
                PublicSummary = "trusted_harness_witness_unavailable"
            });
        }

        if (observedVaultGeneration < witness.Generation)
        {
            return Task.FromResult(new Av3TrustedAnchorVerification
            {
                Verified = false,
                Witness = witness,
                AnchorStatus = Av3AnchorStatus.AnchorRollbackSuspected,
                FailureReason = Av3TrustedAnchorFailureReason.ExternalWitnessCounterRollback,
                FullVaultRollbackSuspected = true,
                PublicSummary = "trusted_harness_generation_rollback"
            });
        }

        if (observedVaultGeneration > witness.Generation)
        {
            return Task.FromResult(new Av3TrustedAnchorVerification
            {
                Verified = false,
                Witness = witness,
                AnchorStatus = Av3AnchorStatus.AnchorStale,
                FailureReason = Av3TrustedAnchorFailureReason.ExternalWitnessCounterStale,
                PublicSummary = "trusted_harness_generation_stale"
            });
        }

        return Task.FromResult(new Av3TrustedAnchorVerification
        {
            Verified = true,
            Witness = witness,
            AnchorStatus = Av3AnchorStatus.AnchorFresh,
            FailureReason = Av3TrustedAnchorFailureReason.None,
            WriterTrustedPromotionAllowed = false,
            ProductionEnableAllowed = false,
            PublicSummary = "trusted_harness_witness_fresh"
        });
    }

    public Task<Av3TrustedAnchorCommitResult> PrepareTrustedAnchorUpdateAsync(
        Av3TrustedAnchorRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureHarnessRoute(request.VaultRoot, request.TestHarnessInvocation);

        var current = ReadStateOrNull(request.VaultRoot);
        var nextCounter = (current?.MonotonicCounter ?? 0UL) + 1UL;
        if (current is not null && request.TargetGeneration < current.Generation)
        {
            return Task.FromResult(Fail(Av3TrustedAnchorFailureReason.ExternalWitnessCounterRollback, "trusted_anchor_monotonicity_violation"));
        }

        if (TestingForceCleanupFailureAfterPrepare)
        {
            return Task.FromResult(Fail(Av3TrustedAnchorFailureReason.CleanupFailureAfterPrepare, "trusted_anchor_cleanup_failed"));
        }

        var pending = new PendingDocument
        {
            UpdateId = request.UpdateId,
            VaultId = request.VaultId,
            AnchorId = request.AnchorId,
            TargetGeneration = request.TargetGeneration,
            MonotonicCounter = nextCounter,
            PreviousWitnessDigestHex = current?.CurrentWitnessDigestHex ?? string.Empty,
            CurrentWitnessDigestHex = request.CurrentWitnessDigestHex,
            HeaderRootDigestHex = request.HeaderRootDigestHex,
            MetadataCiphertextDigestHex = request.MetadataCiphertextDigestHex,
            ActivationDigestHex = request.ActivationDigestHex
        };

        WritePending(request.VaultRoot, pending);
        return Task.FromResult(new Av3TrustedAnchorCommitResult
        {
            Success = true,
            Committed = false,
            FailureReason = Av3TrustedAnchorFailureReason.None,
            PublicErrorClass = "ok",
            Witness = ToWitness(pending, current is null)
        });
    }

    public async Task<Av3TrustedAnchorCommitResult> CommitTrustedAnchorUpdateAsync(
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
            return Fail(Av3TrustedAnchorFailureReason.TrustedAnchorUpdateNotCommitted, "trusted_anchor_pending_missing");
        }

        var state = new StateDocument
        {
            VaultId = pending.VaultId,
            AnchorId = pending.AnchorId,
            Generation = pending.TargetGeneration,
            MonotonicCounter = pending.MonotonicCounter,
            PreviousWitnessDigestHex = pending.PreviousWitnessDigestHex,
            CurrentWitnessDigestHex = pending.CurrentWitnessDigestHex,
            HeaderRootDigestHex = pending.HeaderRootDigestHex,
            MetadataCiphertextDigestHex = pending.MetadataCiphertextDigestHex,
            ActivationDigestHex = pending.ActivationDigestHex
        };

        await WriteStateAsync(vaultRoot, state, cancellationToken).ConfigureAwait(false);
        DeletePending(vaultRoot);
        return new Av3TrustedAnchorCommitResult
        {
            Success = true,
            Committed = true,
            FailureReason = Av3TrustedAnchorFailureReason.None,
            PublicErrorClass = "ok",
            Witness = ToWitness(state)
        };
    }

    public Task AbortTrustedAnchorUpdateAsync(string vaultRoot, Guid updateId, CancellationToken cancellationToken = default)
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

    internal static int TestingHoldCommitMilliseconds;

    internal static bool TestingForceCleanupFailureAfterPrepare;

    internal static void ClearHarnessState(string vaultRoot) =>
        SharedGuard.PurgeRootHarnessState(vaultRoot);

    private static void EnsureHarnessRoute(string vaultRoot, bool testHarnessInvocation = true)
    {
        if (!Av3PhaseGate.ProductionAnchorImplemented)
        {
            if (!testHarnessInvocation || !Av3TrustedAnchorRuntimePolicy.HarnessTrustedAnchorEnabled)
            {
                throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorAnchorProductionDisabled);
            }
        }

        Av3TrustedAnchorHarnessScope.Ensure(vaultRoot, testHarnessInvocation);
    }

    private static Av3TrustedAnchorWitness? ReadStateOrNull(string vaultRoot)
    {
        var path = StatePath(vaultRoot);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var doc = JsonSerializer.Deserialize<StateDocument>(File.ReadAllText(path));
            return doc is null ? null : ToWitness(doc);
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
            return JsonSerializer.Deserialize<PendingDocument>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    private static void WritePending(string vaultRoot, PendingDocument pending)
    {
        var dir = Av3TrustedAnchorHarnessScope.ResolveTrustedStoreDirectory(vaultRoot);
        Directory.CreateDirectory(dir);
        File.WriteAllText(PendingPath(vaultRoot), JsonSerializer.Serialize(pending));
    }

    private static async Task WriteStateAsync(string vaultRoot, StateDocument state, CancellationToken cancellationToken)
    {
        var dir = Av3TrustedAnchorHarnessScope.ResolveTrustedStoreDirectory(vaultRoot);
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(StatePath(vaultRoot), JsonSerializer.Serialize(state), cancellationToken).ConfigureAwait(false);
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
        Path.Combine(Av3TrustedAnchorHarnessScope.ResolveTrustedStoreDirectory(vaultRoot), Av3TrustedAnchorRuntimePolicy.TrustedStateFileName);

    private static string PendingPath(string vaultRoot) =>
        Path.Combine(Av3TrustedAnchorHarnessScope.ResolveTrustedStoreDirectory(vaultRoot), Av3TrustedAnchorRuntimePolicy.TrustedPendingFileName);

    private static Av3TrustedAnchorWitness ToWitness(StateDocument doc) =>
        new()
        {
            VaultId = doc.VaultId,
            AnchorId = doc.AnchorId,
            Generation = doc.Generation,
            MonotonicCounter = doc.MonotonicCounter,
            PreviousWitnessDigestHex = doc.PreviousWitnessDigestHex,
            CurrentWitnessDigestHex = doc.CurrentWitnessDigestHex,
            HeaderRootDigestHex = doc.HeaderRootDigestHex,
            MetadataCiphertextDigestHex = doc.MetadataCiphertextDigestHex,
            ActivationDigestHex = doc.ActivationDigestHex,
            ProviderKind = Av3TrustedAnchorProviderKind.HarnessSynthetic,
            MachineBindingState = Av3TrustedAnchorBindingState.Bound,
            ExternalWitnessState = Av3TrustedAnchorExternalState.Synchronized,
            OfflineGraceState = Av3TrustedAnchorOfflineState.Online,
            RecoveryState = Av3TrustedAnchorRecoveryState.None
        };

    private static Av3TrustedAnchorWitness ToWitness(PendingDocument pending, bool genesis) =>
        new()
        {
            VaultId = pending.VaultId,
            AnchorId = pending.AnchorId,
            Generation = pending.TargetGeneration,
            MonotonicCounter = pending.MonotonicCounter,
            PreviousWitnessDigestHex = pending.PreviousWitnessDigestHex,
            CurrentWitnessDigestHex = pending.CurrentWitnessDigestHex,
            HeaderRootDigestHex = pending.HeaderRootDigestHex,
            MetadataCiphertextDigestHex = pending.MetadataCiphertextDigestHex,
            ActivationDigestHex = pending.ActivationDigestHex,
            ProviderKind = Av3TrustedAnchorProviderKind.HarnessSynthetic,
            MachineBindingState = Av3TrustedAnchorBindingState.Bound,
            ExternalWitnessState = genesis ? Av3TrustedAnchorExternalState.Unknown : Av3TrustedAnchorExternalState.Synchronized,
            OfflineGraceState = Av3TrustedAnchorOfflineState.Online,
            RecoveryState = Av3TrustedAnchorRecoveryState.None
        };

    private static Av3TrustedAnchorCommitResult Fail(Av3TrustedAnchorFailureReason reason, string code) =>
        new()
        {
            Success = false,
            Committed = false,
            FailureReason = reason,
            PublicErrorClass = code
        };

    private sealed class StateDocument
    {
        public Guid VaultId { get; set; }

        public Guid AnchorId { get; set; }

        public ulong Generation { get; set; }

        public ulong MonotonicCounter { get; set; }

        public string PreviousWitnessDigestHex { get; set; } = string.Empty;

        public string CurrentWitnessDigestHex { get; set; } = string.Empty;

        public string HeaderRootDigestHex { get; set; } = string.Empty;

        public string MetadataCiphertextDigestHex { get; set; } = string.Empty;

        public string ActivationDigestHex { get; set; } = string.Empty;
    }

    private sealed class PendingDocument
    {
        public Guid UpdateId { get; set; }

        public Guid VaultId { get; set; }

        public Guid AnchorId { get; set; }

        public ulong TargetGeneration { get; set; }

        public ulong MonotonicCounter { get; set; }

        public string PreviousWitnessDigestHex { get; set; } = string.Empty;

        public string CurrentWitnessDigestHex { get; set; } = string.Empty;

        public string HeaderRootDigestHex { get; set; } = string.Empty;

        public string MetadataCiphertextDigestHex { get; set; } = string.Empty;

        public string ActivationDigestHex { get; set; } = string.Empty;
    }
}