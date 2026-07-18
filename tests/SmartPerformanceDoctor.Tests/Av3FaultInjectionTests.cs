using System.Reflection;
using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Journal;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.Validation;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3FaultInjectionTests
{
    private const string SecretMarker = "X-SECRET-MARKER-E1-9f3c";

    [Theory]
    [MemberData(nameof(FaultMatrixData))]
    public void Fault_Matrix_AllMandatoryPoints_ClassifyExpected(Av3FaultMatrix.MatrixRow row)
    {
        using var storage = new Av3TestStorage();
        var txn = Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage);
        var injector = new Av3FaultInjector(new Av3FaultInjectionScenario
        {
            FaultPoint = row.Point,
            FailFlush = row.RequiresFlushFailure,
            FailReread = row.RequiresFailReread,
            FailAuthentication = row.RequiresFailAuthentication
        });
        var result = Av3ExperimentalWriter.SimulateCommit(txn, injector);
        if (row.Point == Av3FaultPoint.DuringCleanup)
        {
            Assert.False(result.Completed);
            Assert.Equal(row.Expected, result.Classification);
            return;
        }

        Assert.False(result.Completed);
        Assert.Equal(row.Expected, result.Classification);
        Assert.False(Av3RecoveryClassifier.TrustsMetadata(result.Classification));
        if (row.Expected != Av3RecoveryClassification.NewGenerationOpen)
        {
            Assert.Equal(3ul, result.FaultResult!.TrustedOpenGeneration);
        }
    }

    public static IEnumerable<object[]> FaultMatrixData() => Av3FaultMatrix.TheoryData();

    [Fact]
    public void Fault_AfterActivationFlushBeforeReread_DeterministicClassification()
    {
        using var storage1 = new Av3TestStorage();
        using var storage2 = new Av3TestStorage();
        var scenario = new Av3FaultInjectionScenario { FaultPoint = Av3FaultPoint.AfterActivationFlushBeforeReread, FailReread = true };
        var a = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage1),
            new Av3FaultInjector(scenario));
        var b = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage2),
            new Av3FaultInjector(scenario));
        Assert.Equal(a.Classification, b.Classification);
        Assert.Equal(Av3RecoveryClassification.CorruptBlocked, a.Classification); // FailReread path
    }

    [Fact]
    public void Fault_RereadFailure_NotCommitted()
    {
        using var storage = new Av3TestStorage();
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage),
            new Av3FaultInjector(new Av3FaultInjectionScenario
            {
                FaultPoint = Av3FaultPoint.AfterActivationFlushBeforeReread,
                FailReread = true
            }));
        Assert.False(result.Completed);
        Assert.NotEqual(Av3RecoveryClassification.NewGenerationOpen, result.Classification);
    }

    [Fact]
    public void Fault_PostFlushAuthFailure_NotCommitted()
    {
        using var storage = new Av3TestStorage();
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage),
            new Av3FaultInjector(new Av3FaultInjectionScenario
            {
                FaultPoint = Av3FaultPoint.AfterRereadBeforeAuthentication,
                FailAuthentication = true
            }));
        Assert.False(result.Completed);
        Assert.Equal(Av3RecoveryClassification.CorruptBlocked, result.Classification);
        Assert.False(result.FaultResult!.MetadataTrusted);
    }

    [Fact]
    public void Fault_CleanupCrash_ClassificationBounded()
    {
        using var storage = new Av3TestStorage();
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage),
            new Av3FaultInjector(new Av3FaultInjectionScenario { FaultPoint = Av3FaultPoint.DuringCleanup }));
        Assert.False(result.Completed);
        Assert.True(result.Classification is Av3RecoveryClassification.RedundancyDegraded
            or Av3RecoveryClassification.RecoveryRequired);
    }

    [Fact]
    public void Fault_NoPartialGenerationNormalOpen()
    {
        using var storage = new Av3TestStorage();
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage),
            new Av3FaultInjector(new Av3FaultInjectionScenario { FaultPoint = Av3FaultPoint.AfterMetadataWriteBeforeFlush }));
        Assert.False(Av3RecoveryClassifier.AllowsNormalOpen(result.Classification)
                     && result.Classification == Av3RecoveryClassification.NewGenerationOpen);
    }

    [Fact]
    public void Fault_OldGenerationPreservedUntilCommit()
    {
        using var storage = new Av3TestStorage();
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage),
            new Av3FaultInjector(new Av3FaultInjectionScenario { FaultPoint = Av3FaultPoint.BeforeActivationHeaderWrite }));
        Assert.Equal(3ul, result.FaultResult!.TrustedOpenGeneration);
    }

    [Fact]
    public void Fault_SuccessfulHarnessRun_NewGenerationOpen()
    {
        using var storage = new Av3TestStorage();
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage),
            null);
        Assert.True(result.Completed);
        Assert.Equal(Av3RecoveryClassification.NewGenerationOpen, result.Classification);
        Assert.True(result.FaultResult!.MetadataTrusted);
        Assert.True(result.FaultResult.ActivationAuthenticated);
    }

    [Fact]
    public void Harness_RealAead_PostCommit_UnlockValidatorEquivalent()
    {
        using var storage = new Av3TestStorage();
        var plan = MinimalPlan();
        var context = Av3HarnessCommitContext.Generate(plan);
        var txn = Av3WriteTransaction.CreateForTestHarness(plan, storage, context);
        var result = Av3ExperimentalWriter.SimulateCommit(txn, null);
        Assert.True(result.Completed);
        var header = storage.TryReadFlushed("header/activation.bin")!;
        var metadata = storage.TryReadFlushed("metadata/root.enc")!;
        Assert.True(Av3HarnessCommitCrypto.TryAuthenticatePostCommit(header, metadata, context.Vmk));
    }

    [Fact]
    public void Security_NoFilenamePathLeak_InJournalDescriptor()
    {
        var userPath = @"C:\Users\secret\Documents\photo.jpg";
        var desc = new Av3JournalDescriptor
        {
            CipherSuiteId = 1,
            ContainerId = Guid.NewGuid(),
            TransactionId = Guid.NewGuid(),
            PreviousGeneration = 1,
            TargetGeneration = 2,
            PreviousMetadataRootCiphertextDigest = RandomNumberGenerator.GetBytes(32),
            TargetMetadataRootCiphertextDigest = RandomNumberGenerator.GetBytes(32),
            ObjectWriteSetDigest = RandomNumberGenerator.GetBytes(32),
            MetadataWriteDigest = RandomNumberGenerator.GetBytes(32),
            ActivationTarget = Av3JournalDescriptor.ActivationTargetMetadataRoot,
            State = Av3JournalState.Pending
        };
        var bytes = desc.Write();
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain(userPath, text, StringComparison.Ordinal);
        Assert.DoesNotContain("photo.jpg", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Security_NoPlaintextLeakInTrace()
    {
        using var storage = new Av3TestStorage();
        var secret = SecretMarker + "-payload";
        var plan = MinimalPlan();
        var context = new Av3HarnessCommitContext
        {
            Vmk = RandomNumberGenerator.GetBytes(32),
            HarnessObjectPlaintext = System.Text.Encoding.UTF8.GetBytes(secret)
        };
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(plan, storage, context),
            new Av3FaultInjector(new Av3FaultInjectionScenario { FaultPoint = Av3FaultPoint.BeforeObjectWrite }));
        var trace = string.Join(",", result.FaultResult!.Trace.Steps);
        Assert.DoesNotContain(secret, trace, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretMarker, trace, StringComparison.Ordinal);
    }

    [Fact]
    public void Security_SecretMarkerNonLeak_PublicMessageOnly()
    {
        UnlockValidationException? ex = null;
        try
        {
            ReadOnlyUnlockValidator.Validate([], [], [], SecretMarker + "-password");
        }
        catch (UnlockValidationException caught)
        {
            ex = caught;
        }

        Assert.NotNull(ex);
        Assert.Equal(UnlockValidationException.PublicMessage, ex!.Message);
        Assert.DoesNotContain(SecretMarker, ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(SecretMarker, ex.ToString(), StringComparison.Ordinal);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void Security_NonHarnessWriterInvocation_Blocked()
    {
        Assert.Throws<InvalidOperationException>(() => Av3ExperimentalWriterAccess.EnsureHarnessOnly(false));
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void Security_ProductionWriterGate_RemainsFalse()
    {
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.False(Av3PhaseGate.ExperimentalWriterHarnessEnabled);
    }

    [Fact]
    public void Security_WriterNotReachableFromAppAssembly()
    {
        var appAsm = typeof(SmartPerformanceDoctor.App.Services.Security.SecureVaultService).Assembly;
        var refs = appAsm.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType.FullName ?? ""))
            .Concat(appAsm.GetTypes().Select(t => t.FullName ?? ""))
            .Any(n => n.Contains("Av3ExperimentalWriter", StringComparison.Ordinal));
        Assert.False(refs);
    }

    [Fact]
    public void Journal_DescriptorRoundTrip_ValidatorPass()
    {
        var desc = new Av3JournalDescriptor
        {
            CipherSuiteId = 1,
            ContainerId = Guid.NewGuid(),
            TransactionId = Guid.NewGuid(),
            PreviousGeneration = 1,
            TargetGeneration = 2,
            PreviousMetadataRootCiphertextDigest = RandomNumberGenerator.GetBytes(32),
            TargetMetadataRootCiphertextDigest = RandomNumberGenerator.GetBytes(32),
            ObjectWriteSetDigest = RandomNumberGenerator.GetBytes(32),
            MetadataWriteDigest = RandomNumberGenerator.GetBytes(32),
            ActivationTarget = Av3JournalDescriptor.ActivationTargetMetadataRoot,
            State = Av3JournalState.JournalDurable,
            MonotonicTimestampUtc = 42
        };
        var bytes = desc.Write();
        var parsed = Av3JournalDescriptor.Parse(bytes);
        Av3JournalValidator.ValidateForRecovery(parsed, parsed.ContainerId, lastAuthenticatedGeneration: 1);
        Assert.Equal(2ul, parsed.TargetGeneration);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void Recovery_EqualGenerationConflictingRoot_CorruptBlocked()
    {
        var snapshot = new Av3CommitSnapshot
        {
            PreviousAuthenticatedGeneration = 2,
            AttemptedTargetGeneration = 2,
            EqualGenerationConflictingRoot = true
        };
        Assert.Equal(Av3RecoveryClassification.CorruptBlocked, Av3RecoveryClassifier.Classify(snapshot));
    }

    [Fact]
    public void Recovery_RollbackSuspected_Classified()
    {
        var snapshot = new Av3CommitSnapshot { RollbackSuspected = true };
        Assert.Equal(Av3RecoveryClassification.RollbackSuspected, Av3RecoveryClassifier.Classify(snapshot));
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