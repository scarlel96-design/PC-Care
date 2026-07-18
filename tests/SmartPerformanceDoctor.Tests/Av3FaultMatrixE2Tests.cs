using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.Validation;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3FaultMatrixE2Tests
{
    private const string SecretMarker = "X-SECRET-MARKER-E2-7a2f";

    [Fact]
    public void Matrix_Runner_AllRows_Pass()
    {
        var report = Av3FaultMatrixRunner.RunAll(MinimalPlan);
        Assert.Equal(report.Total, report.Passed);
        Assert.Equal(0, report.Failed);
    }

    [Fact]
    public void Matrix_Report_NoSecretLeak()
    {
        var report = Av3FaultMatrixRunner.RunAll(MinimalPlan);
        var json = report.ToSafeJson();
        var vmk = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        Assert.False(Av3FaultMatrixReport.ContainsForbiddenLeak(
            json,
            [SecretMarker, "password", "VMK", "DEK", vmk, @"C:\Users\", "spd-vault", ".svdb"]));
    }

    [Theory]
    [MemberData(nameof(CrashSafeData))]
    public void CrashSafe_Scenario_ClassifiesExpected(Av3CrashSafeScenarioMatrix.ScenarioRow row)
    {
        if (row.ClassifierOnly)
        {
            var entry = Av3FaultMatrixRunner.RunAll(MinimalPlan).Entries
                .First(e => e.ScenarioId == $"crash_safe_{(int)row.Scenario}");
            Assert.True(entry.Pass);
            return;
        }

        using var storage = row.Scenario == Av3CrashSafeScenario.CleanupDuringCrash
            ? Av3TestStorage.CreateIsolatedCleanupRoot()
            : new Av3TestStorage();
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage),
            new Av3FaultInjector(new Av3FaultInjectionScenario
            {
                FaultPoint = row.FaultPoint!.Value,
                FailFlush = row.FailFlush,
                FailReread = row.FailReread,
                FailAuthentication = row.FailAuthentication
            }));
        Assert.Equal(row.Expected, result.Classification);
    }

    public static IEnumerable<object[]> CrashSafeData() => Av3CrashSafeScenarioMatrix.TheoryData();

    [Fact]
    public void Flush_Harness_ObjectFlushFailure_Aborted()
    {
        using var storage = new Av3TestStorage();
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage),
            new Av3FaultInjector(new Av3FaultInjectionScenario
            {
                FaultPoint = Av3FaultPoint.AfterObjectWriteBeforeFlush,
                FailFlush = true
            }));
        Assert.Equal(Av3RecoveryClassification.Aborted, result.Classification);
    }

    [Fact]
    public void ProcessKill_Harness_SimulatedOnly_FlagDocumented()
    {
        if (OperatingSystem.IsWindows() && Av3ChildProcessKillHarness.IsSupported)
        {
            Assert.True(Av3ProcessKillHarness.ActualProcessKillSupported);
            return;
        }

        Assert.False(Av3ProcessKillHarness.ActualProcessKillSupported);
    }

    [Fact]
    public void PartialGeneration_MetadataUntrustedBeforeActivationAuth()
    {
        using var storage = new Av3TestStorage();
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage),
            new Av3FaultInjector(new Av3FaultInjectionScenario
            {
                FaultPoint = Av3FaultPoint.BeforeActivationHeaderWrite
            }));
        Assert.False(result.FaultResult!.MetadataTrusted);
        Assert.False(result.FaultResult.ActivationAuthenticated);
    }

    [Fact]
    public void PartialGeneration_FlushFailure_NotCommitted()
    {
        using var storage = new Av3TestStorage();
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage),
            new Av3FaultInjector(new Av3FaultInjectionScenario
            {
                FaultPoint = Av3FaultPoint.AfterMetadataWriteBeforeFlush,
                FailFlush = true
            }));
        Assert.NotEqual(Av3RecoveryClassification.NewGenerationOpen, result.Classification);
        Assert.False(Av3RecoveryClassifier.AllowsNormalOpen(result.Classification)
                     && result.Classification == Av3RecoveryClassification.NewGenerationOpen);
    }

    [Fact]
    public void PartialGeneration_UnauthenticatedHighGeneration_Rejected()
    {
        var snapshot = new Av3CommitSnapshot { StaleHighGenerationUnauthenticated = true };
        Assert.Equal(Av3RecoveryClassification.PreviousGenerationOpen, Av3RecoveryClassifier.Classify(snapshot));
        Assert.False(Av3RecoveryClassifier.TrustsMetadata(Av3RecoveryClassifier.Classify(snapshot)));
    }

    [Fact]
    public void Isolation_TestRoot_UsesE2Prefix()
    {
        using var storage = new Av3TestStorage();
        Assert.StartsWith(Av3TestStorage.RootPrefix, Path.GetFileName(storage.RootPath), StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => storage.WritePending("../escape.bin", [1]));
    }

    [Fact]
    public void Isolation_CleanupCrash_UsesDedicatedRoot()
    {
        var root = Av3TestStorage.CreateIsolatedCleanupRoot().RootPath;
        Assert.Contains("cleanup-", root, StringComparison.Ordinal);
    }

    [Fact]
    public void Security_HostService_NoExperimentalWriterReference()
    {
        var hostAsm = typeof(SmartPerformanceDoctor.App.Services.Security.AstraVaultHostService).Assembly;
        var hit = hostAsm.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType.FullName ?? ""))
            .Concat(hostAsm.GetTypes().Select(t => t.FullName ?? ""))
            .Any(n => n.Contains("Av3ExperimentalWriter", StringComparison.Ordinal));
        Assert.False(hit);
    }

    [Fact]
    public void Harness_FullAuthChain_SucceedsOnHappyPath()
    {
        using var storage = new Av3TestStorage();
        var plan = MinimalPlan();
        var ctx = Av3HarnessCommitContext.Generate(plan);
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(plan, storage, ctx), null);
        Assert.True(result.Completed);
        var header = storage.TryReadFlushed("header/activation.bin")!;
        var meta = storage.TryReadFlushed("metadata/root.enc")!;
        var auth = Av3HarnessPostCommitAuthenticator.AuthenticateFullChain(header, meta, ctx.Vmk);
        Assert.True(auth.Success);
        Assert.True(auth.ActivationAeadAuthenticated);
        Assert.True(auth.MetadataRootAeadAuthenticated);
        Assert.True(auth.GenerationRollbackValidated);
    }

    [Fact]
    public void Golden_MetadataRootAad_CrossCheck_FromVectors()
    {
        var outputDir = Path.Combine(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "av3-vectors")), "reference-output");
        if (!Directory.Exists(outputDir))
        {
            return;
        }

        var expected = File.ReadAllBytes(Path.Combine(outputDir, "metadata-root-aad.bin"));
        var containerId = new Guid("a3f1c2e4-5b6d-4789-a012-3456789abcde");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDir, "metadata-root-expected-result.json")));
        static byte[] Hex(string h)
        {
            var bytes = new byte[h.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(h.AsSpan(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
            }

            return bytes;
        }

        var digest = Hex(doc.RootElement.GetProperty("metadata_ciphertext_digest_hex").GetString()!);
        var activationDigest = SHA256.HashData(File.ReadAllBytes(Path.Combine(outputDir, "activation-payload-plaintext.bin")));
        var built = SmartPerformanceDoctor.AstraVault.Crypto.MetadataRootAad.Build(
            3, 1, containerId, containerId, 42, 42, digest, activationDigest, 1, 512);
        Assert.Equal(expected, built);
    }

    private static Av3WritePlan MinimalPlan() => new()
    {
        ContainerId = Guid.NewGuid(),
        TransactionId = Guid.NewGuid(),
        PreviousGeneration = 3,
        TargetGeneration = 4,
        PreviousMetadataRootDigest = RandomNumberGenerator.GetBytes(32),
        Objects = new Av3ObjectWriteSet { ObjectWriteSetDigest = RandomNumberGenerator.GetBytes(32) },
        Metadata = new Av3MetadataWriteSet
        {
            MetadataWriteDigest = RandomNumberGenerator.GetBytes(32),
            TargetMetadataRootCiphertextDigest = RandomNumberGenerator.GetBytes(32)
        }
    };
}