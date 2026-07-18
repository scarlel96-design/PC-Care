using System.Reflection;
using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Durable;
using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.Experimental.HeaderCopy;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;
using SmartPerformanceDoctor.AstraVault.Repair;
using SmartPerformanceDoctor.AstraVault.Target;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3PhaseE3Tests
{
    private const string SecretMarker = "X-SECRET-MARKER-E3-4c91";

    private static bool KillSupported => Av3ChildProcessKillHarness.IsSupported;

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void PhaseGate_E3_ActualKillHarness_DocumentationOnly()
    {
        Assert.Contains("PRODUCTION AUTHORIZED", Av3PhaseGate.PhaseLabel, StringComparison.Ordinal);
        Assert.True(Av3PhaseGate.ActualKillHarnessEnabled);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.False(Av3PhaseGate.ExperimentalWriterHarnessEnabled);
    }

    [Fact]
    public void Kill_BeforeObjectWrite_ClassifiesPreviousGeneration()
    {
        if (!KillSupported)
        {
            Assert.NotEqual(Av3KillSupportStatus.Supported, Av3ChildProcessKillHarness.SupportStatus);
            return;
        }
        var r = Av3ChildProcessKillHarness.Run(new Av3KillScenario { KillMarker = Av3FaultPoint.BeforeObjectWrite });
        Assert.True(r.ChildKilled);
        Assert.Equal(Av3RecoveryClassification.PreviousGenerationOpen, r.Classification);
    }

    [Fact]
    public void Kill_AfterObjectWriteBeforeFlush_NotCommitted()
    {
        if (!AssertKillSupported())
        {
            return;
        }

        var r = Av3ChildProcessKillHarness.Run(new Av3KillScenario { KillMarker = Av3FaultPoint.AfterObjectWriteBeforeFlush });
        Assert.NotEqual(Av3RecoveryClassification.NewGenerationOpen, r.Classification);
    }

    [Fact]
    public void Kill_AfterMetadataFlush_RecoveryClass()
    {
        if (!AssertKillSupported())
        {
            return;
        }

        var r = Av3ChildProcessKillHarness.Run(new Av3KillScenario { KillMarker = Av3FaultPoint.AfterMetadataFlush });
        Assert.Equal(Av3RecoveryClassification.RecoveryRequired, r.Classification);
    }

    [Fact]
    public void Kill_AfterJournalFlush_RecoveryClass()
    {
        if (!AssertKillSupported())
        {
            return;
        }

        var r = Av3ChildProcessKillHarness.Run(new Av3KillScenario { KillMarker = Av3FaultPoint.AfterJournalFlush });
        Assert.Equal(Av3RecoveryClassification.RecoveryRequired, r.Classification);
    }

    [Fact]
    public void Kill_AfterActivationHeaderBeforeFlush_RecoveryClass()
    {
        if (!AssertKillSupported())
        {
            return;
        }

        var r = Av3ChildProcessKillHarness.Run(new Av3KillScenario { KillMarker = Av3FaultPoint.AfterActivationHeaderWriteBeforeFlush });
        Assert.Equal(Av3RecoveryClassification.RecoveryRequired, r.Classification);
    }

    [Fact]
    public void Kill_AfterActivationFlushBeforeReread_NotCommitted()
    {
        if (!AssertKillSupported())
        {
            return;
        }

        var r = Av3ChildProcessKillHarness.Run(new Av3KillScenario { KillMarker = Av3FaultPoint.AfterActivationFlushBeforeReread });
        Assert.NotEqual(Av3RecoveryClassification.NewGenerationOpen, r.Classification);
    }

    [Fact]
    public void Kill_AfterRereadBeforeAuth_NotCommitted()
    {
        if (!AssertKillSupported())
        {
            return;
        }

        var r = Av3ChildProcessKillHarness.Run(new Av3KillScenario { KillMarker = Av3FaultPoint.AfterRereadBeforeAuthentication });
        Assert.NotEqual(Av3RecoveryClassification.NewGenerationOpen, r.Classification);
    }

    [Fact]
    public void Kill_DuringCleanup_RecoveryRequired()
    {
        if (!AssertKillSupported())
        {
            return;
        }

        var r = Av3ChildProcessKillHarness.Run(new Av3KillScenario { KillMarker = Av3FaultPoint.DuringCleanup });
        Assert.Equal(Av3RecoveryClassification.RecoveryRequired, r.Classification);
    }

    [Fact]
    public void Durable_FlushFailure_NotCommitted()
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
    }

    [Fact]
    public void Durable_PostFlushRereadFailure_NotCommitted()
    {
        using var storage = new Av3TestStorage();
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage),
            new Av3FaultInjector(new Av3FaultInjectionScenario
            {
                FaultPoint = Av3FaultPoint.AfterActivationFlushBeforeReread,
                FailReread = true
            }));
        Assert.NotEqual(Av3RecoveryClassification.NewGenerationOpen, result.Classification);
        Assert.Equal(Av3RecoveryClassification.CorruptBlocked, result.Classification);
    }

    [Fact]
    public void Durable_PostFlushAuthFailure_NotCommitted()
    {
        using var storage = new Av3TestStorage();
        var result = Av3ExperimentalWriter.SimulateCommit(
            Av3WriteTransaction.CreateForTestHarness(MinimalPlan(), storage),
            new Av3FaultInjector(new Av3FaultInjectionScenario
            {
                FaultPoint = Av3FaultPoint.AfterRereadBeforeAuthentication,
                FailAuthentication = true
            }));
        Assert.Equal(Av3RecoveryClassification.CorruptBlocked, result.Classification);
    }

    [Fact]
    public void HeaderCopy_OneDurable_RedundancyDegraded()
    {
        var root = Path.Combine(Path.GetTempPath(), "av3-e3-header-" + Guid.NewGuid().ToString("N"));
        try
        {
            var state = Av3HeaderCopyWriterHarness.WriteThreeCopies(
                root,
                new Av3HeaderCopyWritePlan { HeaderCopyBytes = RandomNumberGenerator.GetBytes(128) },
                durableCopy0: true,
                durableCopy1: false,
                durableCopy2: false);
            Assert.Equal(Av3RecoveryClassification.RedundancyDegraded,
                Av3HeaderCopyRecoveryClassifier.Classify(state, activationAuthenticated: true));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void HeaderCopy_TwoDurable_NewGenerationOpen()
    {
        var root = Path.Combine(Path.GetTempPath(), "av3-e3-header-" + Guid.NewGuid().ToString("N"));
        try
        {
            var state = Av3HeaderCopyWriterHarness.WriteThreeCopies(
                root,
                new Av3HeaderCopyWritePlan { HeaderCopyBytes = RandomNumberGenerator.GetBytes(128) },
                durableCopy0: true,
                durableCopy1: true,
                durableCopy2: false);
            Assert.Equal(Av3RecoveryClassification.NewGenerationOpen,
                Av3HeaderCopyRecoveryClassifier.Classify(state, activationAuthenticated: true));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void HeaderCopy_Conflict_CorruptBlocked()
    {
        var root = Path.Combine(Path.GetTempPath(), "av3-e3-header-" + Guid.NewGuid().ToString("N"));
        try
        {
            var state = Av3HeaderCopyWriterHarness.WriteThreeCopies(
                root,
                new Av3HeaderCopyWritePlan { HeaderCopyBytes = RandomNumberGenerator.GetBytes(128) },
                durableCopy0: true,
                durableCopy1: true,
                durableCopy2: true,
                conflictingCopy2Bytes: RandomNumberGenerator.GetBytes(128));
            Assert.Equal(Av3RecoveryClassification.CorruptBlocked,
                Av3HeaderCopyRecoveryClassifier.Classify(state, activationAuthenticated: true));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void Repair_RecoveryRequired_MapsRepairRequired()
    {
        Assert.Equal(Av3RepairClassification.RepairRequired,
            Av3RepairClassifier.FromRecovery(Av3RecoveryClassification.RecoveryRequired));
    }

    [Fact]
    public void Repair_RedundancyDegraded_Classified()
    {
        Assert.Equal(Av3RepairClassification.RedundancyDegraded,
            Av3RepairClassifier.FromRecovery(Av3RecoveryClassification.RedundancyDegraded));
    }

    [Fact]
    public void ActualKill_Matrix_CompareSimulated_ReportsSafeJson()
    {
        if (!AssertKillSupported())
        {
            return;
        }

        var report = Av3ActualKillMatrixRunner.RunCompareAll(MinimalPlan);
        Assert.Equal(Av3KillSupportStatus.Supported, report.SupportStatus);
        Assert.Equal(Av3KillMarker.All.Length, report.Total);
        Assert.Equal(0, report.Mismatched);
        Assert.Equal(report.Total, report.Passed);
        var json = report.ToSafeJson();
        Assert.False(Av3KillReport.ContainsForbiddenLeak(json, [SecretMarker, "password", "VMK", "DEK", @"C:\Users\", "spd-vault"]));
    }

    [Fact]
    public void Kill_UnsupportedPlatform_ReportsUnsupportedNotSkipped()
    {
        if (OperatingSystem.IsWindows() && KillSupported)
        {
            return;
        }

        var status = Av3ChildProcessKillHarness.SupportStatus;
        Assert.NotEqual(Av3KillSupportStatus.Supported, status);
        var report = Av3ActualKillMatrixRunner.RunCompareAll(MinimalPlan);
        Assert.True(report.SupportStatus is Av3KillSupportStatus.UnsupportedPlatform
            or Av3KillSupportStatus.WorkerNotFound
            or Av3KillSupportStatus.Blocked);
    }

    [Fact]
    public void Security_AppAndHost_NoHarnessWriter()
    {
        foreach (var asm in new[]
                 {
                     typeof(SmartPerformanceDoctor.App.Services.Security.SecureVaultService).Assembly,
                     typeof(SmartPerformanceDoctor.App.Services.Security.AstraVaultHostService).Assembly
                 })
        {
            var hit = asm.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                .SelectMany(m => m.GetParameters().Select(p => p.ParameterType.FullName ?? ""))
                .Concat(asm.GetTypes().Select(t => t.FullName ?? ""))
                .Any(n => n.Contains("Av3ExperimentalWriter", StringComparison.Ordinal)
                          || n.Contains("Av3DurableStorageHarness", StringComparison.Ordinal)
                          || n.Contains("Av3ChildProcessKillHarness", StringComparison.Ordinal));
            Assert.False(hit);
        }
    }

    private static bool AssertKillSupported()
    {
        if (KillSupported)
        {
            return true;
        }

        Assert.NotEqual(Av3KillSupportStatus.Supported, Av3ChildProcessKillHarness.SupportStatus);
        return false;
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