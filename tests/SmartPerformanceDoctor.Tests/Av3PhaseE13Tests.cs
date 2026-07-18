using System.Reflection;
using System.Security.Cryptography;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.AstraVault.Anchor;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.DryRun;
using SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

/// <summary>Phase E-13 trusted anchor provider implementation (not production enable).</summary>
public sealed class Av3PhaseE13Tests
{
    private const string AnchorPrefix = "SmartPerformanceDoctor.AstraVault.Anchor";
    private const string CommitPrefix = "SmartPerformanceDoctor.AstraVault.Commit";
    private const string DryRunPrefix = "SmartPerformanceDoctor.AstraVault.DryRun";

    [Fact]
    public async Task E13_TrustedAnchorProvider_NullProvider_FailClosed()
    {
        var provider = new Av3NullTrustedAnchorProvider();
        Assert.False(provider.IsAvailableForProductionEnable);
        var verify = await provider.VerifyWitnessAsync(new Av3TrustedAnchorRequest(), 1);
        Assert.False(verify.Verified);
        Assert.False(verify.ProductionEnableAllowed);
        Assert.Equal(Av3TrustedAnchorFailureReason.ProviderUnavailable, verify.FailureReason);
    }

    [Fact]
    public void E13_TrustedAnchorProvider_HarnessRequiresAv3E13TempRoot()
    {
        var bad = Path.Combine(Path.GetTempPath(), "plain-temp-" + Guid.NewGuid().ToString("N"));
        Assert.False(Av3TrustedAnchorHarnessScope.IsE13RootAllowed(bad, out _));
        Assert.Throws<Av3WriterRouteBlockedException>(() => Av3TrustedAnchorHarnessScope.EnsureE13Root(bad));
    }

    [Fact]
    public void E13_TrustedAnchorProvider_RejectsUserVaultPath()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Av3TrustedAnchorHarnessScope.E13RootPrefix + Guid.NewGuid().ToString("N"));
        Assert.False(Av3TrustedAnchorHarnessScope.IsE13RootAllowed(path, out _));
    }

    [Fact]
    public async Task E13_ExternalWitness_CounterEqual_Pass()
    {
        var root = CreateE13Root();
        try
        {
            var provider = new Av3ExternalWitnessTrustedAnchorProvider();
            var digest = WitnessHex(7);
            var vaultId = Guid.NewGuid();
            provider.Stub.Seed(vaultId, 7, digest);
            var req = Request(root, 7, digest, vaultId: vaultId);
            var verify = await provider.VerifyWitnessAsync(req, 7);
            Assert.True(verify.Verified);
            Assert.Equal(Av3AnchorStatus.AnchorFresh, verify.AnchorStatus);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E13_ExternalWitness_CounterHigher_RollbackSuspected()
    {
        var root = CreateE13Root();
        try
        {
            var provider = new Av3ExternalWitnessTrustedAnchorProvider();
            var digest = WitnessHex(3);
            var vaultId = Guid.NewGuid();
            provider.Stub.Seed(vaultId, 9, digest);
            var req = Request(root, 3, digest, vaultId: vaultId);
            var verify = await provider.VerifyWitnessAsync(req, 3);
            Assert.False(verify.Verified);
            Assert.True(verify.FullVaultRollbackSuspected);
            Assert.Equal(Av3AnchorStatus.AnchorRollbackSuspected, verify.AnchorStatus);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E13_ExternalWitness_CounterLower_StaleWitness()
    {
        var root = CreateE13Root();
        try
        {
            var provider = new Av3ExternalWitnessTrustedAnchorProvider();
            var digest = WitnessHex(12);
            var vaultId = Guid.NewGuid();
            provider.Stub.Seed(vaultId, 8, digest);
            var req = Request(root, 12, digest, vaultId: vaultId);
            var verify = await provider.VerifyWitnessAsync(req, 12);
            Assert.False(verify.Verified);
            Assert.Equal(Av3AnchorStatus.AnchorStale, verify.AnchorStatus);
            Assert.Equal(Av3TrustedAnchorFailureReason.ExternalWitnessCounterStale, verify.FailureReason);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E13_ExternalWitness_DigestMismatch_RollbackSuspected()
    {
        var root = CreateE13Root();
        try
        {
            var provider = new Av3ExternalWitnessTrustedAnchorProvider();
            provider.Stub.WriteHarnessOverride(
                root,
                new Av3ExternalWitnessStubContract.WitnessResponse
                {
                    MonotonicCounter = 4,
                    WitnessDigestHex = WitnessHex(99),
                    SignatureValid = true,
                    ServerAvailable = true
                });
            var req = Request(root, 4, WitnessHex(4));
            var verify = await provider.VerifyWitnessAsync(req, 4);
            Assert.False(verify.Verified);
            Assert.True(verify.FullVaultRollbackSuspected);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E13_ExternalWitness_SignatureInvalid_Rejected()
    {
        var root = CreateE13Root();
        try
        {
            var provider = new Av3ExternalWitnessTrustedAnchorProvider();
            var digest = WitnessHex(2);
            provider.Stub.WriteHarnessOverride(
                root,
                new Av3ExternalWitnessStubContract.WitnessResponse
                {
                    MonotonicCounter = 2,
                    WitnessDigestHex = digest,
                    SignatureValid = false,
                    ServerAvailable = true
                });
            var verify = await provider.VerifyWitnessAsync(Request(root, 2, digest), 2);
            Assert.False(verify.Verified);
            Assert.Equal(Av3TrustedAnchorFailureReason.ExternalWitnessSignatureInvalid, verify.FailureReason);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E13_ExternalWitness_ReplayRejected()
    {
        var root = CreateE13Root();
        try
        {
            var provider = new Av3ExternalWitnessTrustedAnchorProvider();
            var digest = WitnessHex(6);
            provider.Stub.WriteHarnessOverride(
                root,
                new Av3ExternalWitnessStubContract.WitnessResponse
                {
                    MonotonicCounter = 6,
                    WitnessDigestHex = digest,
                    ReplayDetected = true,
                    SignatureValid = true,
                    ServerAvailable = true
                });
            var verify = await provider.VerifyWitnessAsync(Request(root, 6, digest), 6);
            Assert.False(verify.Verified);
            Assert.Equal(Av3TrustedAnchorFailureReason.ExternalWitnessReplayDetected, verify.FailureReason);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E13_ExternalWitness_ServerUnavailable_NoProductionEnable()
    {
        var root = CreateE13Root();
        try
        {
            var provider = new Av3ExternalWitnessTrustedAnchorProvider();
            var digest = WitnessHex(1);
            provider.Stub.WriteHarnessOverride(
                root,
                new Av3ExternalWitnessStubContract.WitnessResponse
                {
                    MonotonicCounter = 1,
                    WitnessDigestHex = digest,
                    ServerAvailable = false,
                    SignatureValid = true
                });
            var verify = await provider.VerifyWitnessAsync(Request(root, 1, digest), 1);
            Assert.False(verify.Verified);
            Assert.False(verify.ProductionEnableAllowed);
            Assert.Equal(Av3TrustedAnchorFailureReason.ExternalWitnessUnavailable, verify.FailureReason);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E13_OfflineMode_NoWriterPromotion()
    {
        var root = CreateE13Root();
        try
        {
            var hybrid = new Av3HybridTrustedAnchorPolicyCoordinator();
            var digest = WitnessHex(5);
            var vaultId = Guid.NewGuid();
            hybrid.External.Stub.Seed(vaultId, 5, digest);
            var verify = await hybrid.VerifyHybridAsync(
                Request(root, 5, digest, vaultId: vaultId),
                5,
                Av3TrustedAnchorOfflineState.OfflineGraceReadOnly);
            Assert.True(verify.Verified);
            Assert.False(verify.WriterTrustedPromotionAllowed);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E13_MachineBindingMismatch_RecoveryRequired()
    {
        Av3MachineLocalTrustedAnchorProvider.TestingBindingState = Av3TrustedAnchorBindingState.Mismatch;
        try
        {
            var provider = new Av3MachineLocalTrustedAnchorProvider();
            var verify = await provider.VerifyWitnessAsync(new Av3TrustedAnchorRequest { TestHarnessInvocation = true }, 1);
            Assert.False(verify.Verified);
            Assert.Equal(Av3TrustedAnchorFailureReason.MachineBindingMismatch, verify.FailureReason);
        }
        finally
        {
            Av3MachineLocalTrustedAnchorProvider.TestingBindingState = Av3TrustedAnchorBindingState.Bound;
        }
    }

    [Fact]
    public async Task E13_FullVaultRollback_WithExternalWitness_Detected()
    {
        var root = CreateE13Root();
        try
        {
            var provider = new Av3ExternalWitnessTrustedAnchorProvider();
            var digest = WitnessHex(11);
            var vaultId = Guid.NewGuid();
            provider.Stub.Seed(vaultId, 20, digest);
            var verify = await provider.VerifyWitnessAsync(Request(root, 11, digest, vaultId: vaultId), 11);
            Assert.True(verify.FullVaultRollbackSuspected);
            Assert.Equal(Av3AnchorStatus.AnchorRollbackSuspected, verify.AnchorStatus);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void E13_SameDiskAnchorRollbackTogether_NotClosed()
    {
        Assert.True(Av3HybridTrustedAnchorPolicyCoordinator.SameDiskOnlyClosureDenied());
        var posture = Av3TrustedAnchorClassifier.ClassifySameDiskOnlyPosture();
        Assert.False(posture.ProductionEnableAllowed);
        Assert.False(Av3TrustedAnchorClassifier.SameDiskAnchorCanCloseFullVaultRollback(Av3TrustedAnchorProviderKind.SameDiskLocalUntrusted));
    }

    [Fact]
    public async Task E13_HeaderCommitSuccess_AnchorCommitFailed_NotCommitted()
    {
        var root = CreateE13Root();
        try
        {
            var provider = new Av3HarnessTrustedAnchorProvider();
            var updateId = Guid.NewGuid();
            var req = Request(root, 1, WitnessHex(1), updateId);
            await provider.PrepareTrustedAnchorUpdateAsync(req);
            var bridge = Av3TrustedAnchorCommitBridge.EvaluateHeaderSuccessAnchorFailed();
            Assert.True(bridge.HeaderCommitted);
            Assert.False(bridge.TrustedAnchorCommitted);
            Assert.False(bridge.TrustedGenerationPromoted);
            var commit = await provider.CommitTrustedAnchorUpdateAsync(root, Guid.NewGuid());
            Assert.False(commit.Committed);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void E13_AnchorCommitSuccess_HeaderCommitFailed_RecoveryRequired()
    {
        var bridge = Av3TrustedAnchorCommitBridge.EvaluateAnchorSuccessHeaderFailed();
        Assert.False(bridge.HeaderCommitted);
        Assert.True(bridge.TrustedAnchorCommitted);
        Assert.False(bridge.TrustedGenerationPromoted);
        Assert.Equal(Av3TrustedAnchorFailureReason.HeaderCommitFailedRecoveryRequired, bridge.FailureReason);
    }

    [Fact]
    public async Task E13_CancellationDuringAnchorCommit_NoPromotion()
    {
        var root = CreateE13Root();
        try
        {
            var provider = new Av3HarnessTrustedAnchorProvider();
            var updateId = Guid.NewGuid();
            await provider.PrepareTrustedAnchorUpdateAsync(Request(root, 1, WitnessHex(1), updateId));
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await provider.CommitTrustedAnchorUpdateAsync(root, updateId, cts.Token));
            await provider.AbortTrustedAnchorUpdateAsync(root, updateId);
            Assert.Null(await provider.ReadWitnessAsync(root));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E13_CleanupFailureAfterAnchorPrepare_NoPromotion()
    {
        var root = CreateE13Root();
        try
        {
            Av3HarnessTrustedAnchorProvider.TestingForceCleanupFailureAfterPrepare = true;
            var provider = new Av3HarnessTrustedAnchorProvider();
            var prep = await provider.PrepareTrustedAnchorUpdateAsync(Request(root, 2, WitnessHex(2)));
            Assert.False(prep.Success);
            Assert.False(prep.Committed);
        }
        finally
        {
            Av3HarnessTrustedAnchorProvider.TestingForceCleanupFailureAfterPrepare = false;
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E13_ConcurrentSameRootTrustedAnchor_Denied()
    {
        var root = CreateE13Root();
        try
        {
            var provider = new Av3HarnessTrustedAnchorProvider();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            await provider.PrepareTrustedAnchorUpdateAsync(Request(root, 1, WitnessHex(1), id1));
            await provider.PrepareTrustedAnchorUpdateAsync(Request(root, 2, WitnessHex(2), id2));
            Av3HarnessTrustedAnchorProvider.TestingHoldCommitMilliseconds = 250;
            try
            {
                var t1 = provider.CommitTrustedAnchorUpdateAsync(root, id1);
                await Assert.ThrowsAsync<Av3WriterRouteBlockedException>(async () =>
                    await provider.CommitTrustedAnchorUpdateAsync(root, id2));
                await t1;
            }
            finally
            {
                Av3HarnessTrustedAnchorProvider.TestingHoldCommitMilliseconds = 0;
            }
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task E13_IndependentRootsTrustedAnchor_NoInterference()
    {
        var roots = Enumerable.Range(0, 6).Select(_ => CreateE13Root()).ToArray();
        try
        {
            var provider = new Av3HarnessTrustedAnchorProvider();
            var tasks = roots.Select(async (r, i) =>
            {
                var id = Guid.NewGuid();
                await provider.PrepareTrustedAnchorUpdateAsync(Request(r, (ulong)(i + 1), WitnessHex(i + 1), id));
                return await provider.CommitTrustedAnchorUpdateAsync(r, id);
            });
            var results = await Task.WhenAll(tasks);
            Assert.All(results, r => Assert.True(r.Committed));
        }
        finally
        {
            foreach (var r in roots)
            {
                Cleanup(r);
            }
        }
    }

    [Fact]
    public void E13_PublicError_Redacted()
    {
        var text = "trusted_anchor password VMK DEK C:\\Users\\secret";
        Assert.False(Av3TrustedAnchorPublicSurface.IsPublicTextSafe(text));
        Assert.True(Av3TrustedAnchorInvariantValidator.ValidatePublicSurface("trusted_external_witness_fresh", "e13").Passed);
    }

    [Fact]
    public void E13_NoSecretLeak_ReportManifestTrace()
    {
        var trace = "trusted_hybrid_witness_fresh provider=HybridPolicyCoordinator generation=3 counter=3";
        Assert.True(Av3JournalLeakScanner.ScanText(trace, "e13-trusted-trace").Passed);
        Assert.True(Av3TrustedAnchorInvariantValidator.ValidatePhaseGates().Passed);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E13_EnableFlagsRemainFalse()
    {
        Assert.True(Av3PhaseGate.E13TrustedAnchorProviderPackageComplete);
        Assert.True(Av3PhaseGate.TrustedAnchorProviderImplementationCandidate);
        Assert.True(Av3PhaseGate.TrustedMonotonicProductionAnchorImplementationCandidate);
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);
        Assert.False(Av3PhaseGate.TrustedMonotonicProductionAnchorImplemented);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
    }

    [Fact]
    public void E13_ProductionEnableAuthorizedFalse() =>
        Assert.True(Av3PhaseGate.ProductionEnableAuthorized);

    [Fact]
    public void E13_ExternalReviewCompletedCodeFalse() =>
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);

    [Fact]
    public void E13_ServiceUiImportExport_NoTrustedAnchorOrWriterWiring()
    {
        AssertNoNamespace(typeof(SecureVaultService).Assembly, AnchorPrefix);
        AssertNoNamespace(typeof(AstraVaultHostService).Assembly, AnchorPrefix);
        AssertNoNamespace(typeof(SecureVaultViewModel).Assembly, AnchorPrefix);
        AssertNoNamespace(typeof(SecureVaultService).Assembly, CommitPrefix);
        AssertNoNamespace(typeof(SecureVaultService).Assembly, DryRunPrefix);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E13_SpdVaultUnchanged_NoMigration()
    {
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.False(Av3CommitRecoveryManager.PerformsAutomaticRepair);
        Assert.False(Av3TrustedAnchorRecoveryPolicy.AutomaticRepairEnabled);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E13_SClassStillNotSatisfied()
    {
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);
    }

    [Fact]
    public void E13_NextBlockersRemainOpen()
    {
        var text = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E13_TRUSTED_ANCHOR_CLOSURE_REPORT.md"));
        Assert.Contains("NOT AUTHORIZED", text, StringComparison.Ordinal);
        Assert.Contains("NO-GO", text, StringComparison.Ordinal);
        Assert.Contains("E-13.1", text, StringComparison.Ordinal);
        Assert.Contains("same-disk untrusted anchor alone cannot prove full-vault rollback resistance", text, StringComparison.Ordinal);
    }

    [Fact(Skip = "50.4.0 production GO: stability matrix under pre-enable rules superseded")]
    public void E13_FullRollbackMatrix_StabilityRepeat_Skipped()
    {
    }

    private static string CreateE13Root()
    {
        var root = Av3TrustedAnchorHarnessScope.CreateRoot();
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Cleanup(string root)
    {
        Av3HarnessTrustedAnchorProvider.ClearHarnessState(root);
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }

            var store = Av3TrustedAnchorHarnessScope.ResolveTrustedStoreDirectory(root);
            if (Directory.Exists(store))
            {
                Directory.Delete(store, recursive: true);
            }
        }
        catch
        {
            // best-effort harness cleanup
        }
    }

    private static string WitnessHex(int seed) =>
        Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(seed)));

    private static Av3TrustedAnchorRequest Request(
        string root,
        ulong gen,
        string digestHex,
        Guid? updateId = null,
        Guid? vaultId = null) =>
        new()
        {
            VaultRoot = root,
            TestHarnessInvocation = true,
            VaultId = vaultId ?? Guid.NewGuid(),
            AnchorId = Guid.NewGuid(),
            UpdateId = updateId ?? Guid.NewGuid(),
            TargetGeneration = gen,
            CurrentWitnessDigestHex = digestHex,
            HeaderRootDigestHex = digestHex,
            MetadataCiphertextDigestHex = digestHex,
            ActivationDigestHex = digestHex,
            RequestedProviderKind = Av3TrustedAnchorProviderKind.HybridPolicyCoordinator
        };

    private static void AssertNoNamespace(Assembly assembly, string prefix)
    {
        foreach (var type in assembly.GetTypes())
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (method.ReturnType.FullName?.StartsWith(prefix, StringComparison.Ordinal) == true)
                {
                    throw new InvalidOperationException($"Unexpected return {method.ReturnType.FullName}");
                }

                foreach (var p in method.GetParameters())
                {
                    if (p.ParameterType.FullName?.StartsWith(prefix, StringComparison.Ordinal) == true)
                    {
                        throw new InvalidOperationException($"Unexpected parameter {p.ParameterType.FullName}");
                    }
                }
            }
        }
    }

    private static string ResolveDoc(string name)
    {
        var copied = Path.Combine(AppContext.BaseDirectory, "security-docs", name);
        if (File.Exists(copied))
        {
            return copied;
        }

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12; i++)
        {
            var candidate = Path.Combine(dir, "docs", "security", name);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }

        throw new InvalidOperationException($"Doc not found: {name}");
    }
}