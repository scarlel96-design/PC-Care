using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Durable;
using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.Experimental.HeaderCopy;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Commit;

public sealed class Av3CommitHeaderCommitter : IAv3HeaderCommitter
{
    private readonly string _vaultRoot;
    private readonly Av3CommitSimulationOptions _simulation;

    public Av3CommitHeaderCommitter(string vaultRoot, Av3CommitSimulationOptions simulation)
    {
        Av3WriterAccessGate.EnsureIsolatedRoot(vaultRoot);
        _vaultRoot = vaultRoot;
        _simulation = simulation;
    }

    public ValueTask<Av3HeaderCommitResult> CommitThreeCopyAsync(
        Av3HeaderCommitPlan plan,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        Av3WriterAccessGate.DenyProductionCreate();
        _ = plan;
        throw new InvalidOperationException("use CommitHarnessThreeCopy");
    }

    internal Av3HeaderCopyDurabilityState CommitHarnessThreeCopy(
        Av3WritePlan writePlan,
        Av3HarnessMetadataArtifacts meta,
        ReadOnlySpan<byte> vmk,
        ushort cipherSuiteId = Av3HarnessCommitCrypto.HarnessCipherSuite)
    {
        var headerBytes = Av3HarnessCommitCrypto.BuildActivationHeaderCopy(
            vmk,
            writePlan,
            meta.CiphertextDigest,
            meta.PlaintextCommitment,
            copyIndex: 0,
            cipherSuiteId);

        var headerPlan = new Av3HeaderCopyWritePlan { HeaderCopyBytes = headerBytes };
        byte[]? conflict = _simulation.HeaderCopyConflict ? RandomNumberGenerator.GetBytes(headerBytes.Length) : null;

        return Av3HeaderCopyWriterHarness.WriteThreeCopies(
            _vaultRoot,
            headerPlan,
            _simulation.DurableHeaderCopy0,
            _simulation.DurableHeaderCopy1,
            _simulation.DurableHeaderCopy2,
            conflict);
    }

    internal static int CountDurable(Av3HeaderCopyDurabilityState state) =>
        (state.Copy0Durable ? 1 : 0) + (state.Copy1Durable ? 1 : 0) + (state.Copy2Durable ? 1 : 0);
}