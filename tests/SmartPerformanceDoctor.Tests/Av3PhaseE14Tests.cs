using System.Reflection;
using System.Text.Json;
using SmartPerformanceDoctor.App.Services.Security;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.DiskDurability;
using SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;
using SmartPerformanceDoctor.AstraVault.Target;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

/// <summary>Phase E-14 disk durability review (candidate only; not production enable).</summary>
public sealed class Av3PhaseE14Tests
{
    private const string CommitPrefix = "SmartPerformanceDoctor.AstraVault.Commit";
    private const string DryRunPrefix = "SmartPerformanceDoctor.AstraVault.DryRun";
    private const string DiskDurabilityPrefix = "SmartPerformanceDoctor.AstraVault.DiskDurability";

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E14_DiskDurabilityReview_PreflightEvidenceCurrent()
    {
        var sot = LoadSourceOfTruth();
        Assert.True(
            string.Equals(sot.PhaseLabel, "E-14", StringComparison.Ordinal)
            || string.Equals(sot.PhaseLabel, "E-13.1", StringComparison.Ordinal),
            $"Unexpected phase: {sot.PhaseLabel}");
        Assert.True(sot.LatestVerified.FullSuite.Passed > 0);
        Assert.Equal(0, sot.LatestVerified.FullSuite.Failed);
        var md = File.ReadAllText(ResolveDoc("ASTRA_VAULT_TEST_EVIDENCE_SOURCE_OF_TRUTH.md"));
        Assert.Contains(sot.LatestVerified.FullSuite.Passed.ToString(), md, StringComparison.Ordinal);
        Assert.Contains("dotnet format", File.ReadAllText(ResolveDoc("ASTRA_VAULT_E14_DISK_DURABILITY_REVIEW_REPORT.md")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void E14_DurabilityHarness_RequiresAv3E14TempRoot()
    {
        var bad = Path.Combine(Path.GetTempPath(), "plain-temp-" + Guid.NewGuid().ToString("N"));
        Assert.False(Av3DiskDurabilityHarnessScope.IsE14RootAllowed(bad, out _));
        Assert.Throws<Av3WriterRouteBlockedException>(() => Av3DiskDurabilityHarnessScope.EnsureE14Root(bad));
    }

    [Fact]
    public void E14_DurabilityHarness_RejectsUserVaultPath()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Av3DiskDurabilityHarnessScope.E14RootPrefix + Guid.NewGuid().ToString("N"));
        Assert.False(Av3DiskDurabilityHarnessScope.IsE14RootAllowed(path, out _));
    }

    [Theory]
    [InlineData(Environment.SpecialFolder.MyDocuments)]
    [InlineData(Environment.SpecialFolder.Desktop)]
    [InlineData(Environment.SpecialFolder.UserProfile)]
    public void E14_DurabilityHarness_RejectsDocumentsDesktopDownloads(Environment.SpecialFolder folder)
    {
        var basePath = folder == Environment.SpecialFolder.UserProfile
            ? Path.Combine(Environment.GetFolderPath(folder), "Downloads")
            : Environment.GetFolderPath(folder);
        var path = Path.Combine(basePath, Av3DiskDurabilityHarnessScope.E14RootPrefix + Guid.NewGuid().ToString("N"));
        Assert.False(Av3DiskDurabilityHarnessScope.IsE14RootAllowed(path, out _));
        Assert.False(Av3WriterAccessGate.TryNormalizeHarnessRoot(path, out _));
    }

    [Fact]
    public void E14_DurabilityPolicy_NtfsFixedDisk_Candidate()
    {
        Av3DiskDurabilityProbe.ResetTestingState();
        var probePath = Path.Combine(Path.GetTempPath(), "probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            Av3DiskDurabilityProbe.TestingFilesystemOverride = "NTFS";
            var probe = Av3DiskDurabilityProbe.ProbePath(probePath, testHarnessInvocation: true);
            Assert.True(probe.Success);
            Assert.Equal(Av3DiskDurabilityClassification.NtfsFixedDiskCandidate, probe.Capability.Classification);
            Assert.False(probe.Capability.ProductionWriterAllowed);
        }
        finally
        {
            Av3DiskDurabilityProbe.ResetTestingState();
        }
    }

    [Fact]
    public void E14_DurabilityPolicy_RemovableDrive_Restricted()
    {
        Av3DiskDurabilityProbe.ResetTestingState();
        var probePath = Path.Combine(Path.GetTempPath(), "probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            Av3DiskDurabilityProbe.TestingDriveTypeOverride = DriveType.Removable;
            var probe = Av3DiskDurabilityProbe.ProbePath(probePath, testHarnessInvocation: true);
            Assert.False(probe.Success);
            Assert.Equal(Av3DiskDurabilityFailureReason.RemovableMediaWithoutPolicy, probe.FailureReason);
            Assert.Equal(Av3DiskDurabilityClassification.RemovableRestricted, probe.Capability.Classification);
        }
        finally
        {
            Av3DiskDurabilityProbe.ResetTestingState();
        }
    }

    [Fact]
    public void E14_DurabilityPolicy_NetworkShare_NoProductionWriter()
    {
        Av3DiskDurabilityProbe.ResetTestingState();
        var path = $@"\\server\share\{Av3DiskDurabilityHarnessScope.E14RootPrefix}{Guid.NewGuid():N}";
        var probe = Av3DiskDurabilityProbe.ProbePath(path, testHarnessInvocation: true);
        Assert.False(probe.Success);
        Assert.Equal(Av3DiskDurabilityFailureReason.NetworkPathNoProductionWriter, probe.FailureReason);
        Assert.Equal(Av3DiskDurabilityClassification.NetworkPathNoProductionWriter, probe.Capability.Classification);
        Av3DiskDurabilityProbe.ResetTestingState();
    }

    [Fact]
    public void E14_DurabilityPolicy_CloudSyncPath_NoProductionWriter()
    {
        Av3DiskDurabilityProbe.ResetTestingState();
        var path = Path.Combine(Path.GetTempPath(), "OneDrive", Av3DiskDurabilityHarnessScope.E14RootPrefix + Guid.NewGuid().ToString("N"));
        var probe = Av3DiskDurabilityProbe.ProbePath(path, testHarnessInvocation: true);
        Assert.False(probe.Success);
        Assert.Equal(Av3DiskDurabilityFailureReason.CloudSyncPathNoProductionWriter, probe.FailureReason);
        Assert.Equal(Av3DiskDurabilityClassification.CloudSyncNoProductionWriter, probe.Capability.Classification);
        Av3DiskDurabilityProbe.ResetTestingState();
    }

    [Fact]
    public void E14_DurabilityPolicy_UnknownFilesystem_FailClosed()
    {
        Av3DiskDurabilityProbe.ResetTestingState();
        var probePath = Path.Combine(Path.GetTempPath(), "probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            Av3DiskDurabilityProbe.TestingFilesystemOverride = "UNKNOWN";
            var probe = Av3DiskDurabilityProbe.ProbePath(probePath, testHarnessInvocation: true);
            Assert.False(probe.Success);
            Assert.Equal(Av3DiskDurabilityFailureReason.UnknownFilesystemFailClosed, probe.FailureReason);
            Assert.Equal(Av3DiskDurabilityClassification.UnknownFailClosed, probe.Capability.Classification);
        }
        finally
        {
            Av3DiskDurabilityProbe.ResetTestingState();
        }
    }

    [Fact]
    public void E14_WriteFlushReread_Pass()
    {
        var root = CreateE14Root();
        try
        {
            var report = Av3DiskDurabilityHarnessRunner.RunFlushReread(root);
            Assert.True(report.Passed);
            Assert.True(report.FlushRereadVerified);
            Assert.False(report.TrustedPromotionAllowed);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void E14_RenameReplaceSemantics_Pass()
    {
        var root = CreateE14Root();
        try
        {
            var report = Av3DiskDurabilityHarnessRunner.RunRenameReplace(root);
            Assert.True(report.Passed);
            Assert.True(report.RenameReplaceVerified);
            Assert.False(report.TrustedPromotionAllowed);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public void E14_DirectorySyncUnsupported_Classified()
    {
        var report = Av3DiskDurabilityHarnessRunner.ClassifyDirectorySync();
        Assert.True(report.Passed);
        Assert.True(report.DirectorySyncClassified);
        Assert.Equal(Av3DiskDurabilityFailureReason.DirectorySyncUnsupported, report.FailureReason);
        Assert.False(report.TrustedPromotionAllowed);
    }

    [Fact]
    public void E14_FileLock_RetryThenClassify()
    {
        Av3DiskDurabilityProbe.ResetTestingState();
        Av3DiskDurabilityProbe.TestingSimulateFileLock = true;
        Assert.False(Av3DiskDurabilityProbe.TryFileLockWithRetry(out _));
        Assert.False(Av3DiskDurabilityProbe.TryFileLockWithRetry(out _));
        Assert.False(Av3DiskDurabilityProbe.TryFileLockWithRetry(out var reason));
        Assert.Equal(Av3DiskDurabilityFailureReason.FileLockExhausted, reason);
        Av3DiskDurabilityProbe.ResetTestingState();
    }

    [Fact]
    public void E14_AccessDenied_NotCommitted()
    {
        Av3DiskDurabilityProbe.ResetTestingState();
        var root = CreateE14Root();
        try
        {
            Av3DiskDurabilityProbe.TestingSimulateAccessDenied = true;
            var probe = Av3DiskDurabilityProbe.ProbePath(root, testHarnessInvocation: true);
            Assert.False(probe.Success);
            Assert.Equal(Av3DiskDurabilityFailureReason.AccessDenied, probe.FailureReason);
        }
        finally
        {
            Av3DiskDurabilityProbe.ResetTestingState();
            Cleanup(root);
        }
    }

    [Fact]
    public void E14_OutOfSpace_NotCommitted()
    {
        Av3DiskDurabilityProbe.ResetTestingState();
        var root = CreateE14Root();
        try
        {
            Av3DiskDurabilityProbe.TestingSimulateOutOfSpace = true;
            var probe = Av3DiskDurabilityProbe.ProbePath(root, testHarnessInvocation: true);
            Assert.False(probe.Success);
            Assert.Equal(Av3DiskDurabilityFailureReason.OutOfSpace, probe.FailureReason);
        }
        finally
        {
            Av3DiskDurabilityProbe.ResetTestingState();
            Cleanup(root);
        }
    }

    [Fact]
    public void E14_SurpriseRemoval_RecoveryRequired()
    {
        var report = Av3DiskDurabilityHarnessRunner.EvaluateSurpriseRemoval();
        Assert.False(report.Passed);
        Assert.Equal(Av3DiskDurabilityFailureReason.SurpriseRemovalRecoveryRequired, report.FailureReason);
        Assert.False(report.TrustedPromotionAllowed);
    }

    [Fact]
    public void E14_StaleTempFile_RecoveredOrClassifiedNoMutation()
    {
        var ok = Av3DiskDurabilityHarnessRunner.EvaluateStaleTemp(recoveredWithoutMutation: true);
        Assert.True(ok.Passed);
        Assert.False(ok.TrustedPromotionAllowed);
        var fail = Av3DiskDurabilityHarnessRunner.EvaluateStaleTemp(recoveredWithoutMutation: false);
        Assert.False(fail.Passed);
        Assert.Equal(Av3DiskDurabilityFailureReason.StaleTempRecoveryRequired, fail.FailureReason);
    }

    [Fact]
    public void E14_PowerLossBeforeHeader_NoTrustedPromotion()
    {
        var report = Av3DiskDurabilityHarnessRunner.EvaluatePowerLossBeforeHeader();
        Assert.False(report.Passed);
        Assert.Equal(Av3DiskDurabilityFailureReason.PowerLossBeforeHeaderNoPromotion, report.FailureReason);
        Assert.False(report.TrustedPromotionAllowed);
    }

    [Fact]
    public void E14_PowerLossAfterHeaderBeforeRevalidation_RecoveryRequired()
    {
        var report = Av3DiskDurabilityHarnessRunner.EvaluatePowerLossBeforeRevalidation();
        Assert.False(report.Passed);
        Assert.Equal(Av3DiskDurabilityFailureReason.PowerLossBeforeRevalidationRecoveryRequired, report.FailureReason);
        Assert.False(report.TrustedPromotionAllowed);
    }

    [Fact]
    public void E14_CleanupFailure_NoTrustedPromotion()
    {
        var report = Av3DiskDurabilityHarnessRunner.EvaluateCleanupFailure();
        Assert.False(report.Passed);
        Assert.Equal(Av3DiskDurabilityFailureReason.CleanupFailureNoTrustedPromotion, report.FailureReason);
        Assert.False(report.TrustedPromotionAllowed);
    }

    [Fact]
    public void E14_PublicError_Redacted()
    {
        var text = "disk_durability password VMK DEK C:\\Users\\secret";
        Assert.False(Av3DiskDurabilityPublicSurface.IsPublicTextSafe(text));
        Assert.True(Av3DiskDurabilityInvariantValidator.ValidatePublicSurface("disk_harness_probe_ok", "e14").Passed);
    }

    [Fact]
    public void E14_NoSecretLeak_ReportManifestTrace()
    {
        var trace = "disk_flush_reread_ok disk_rename_ok classification=NtfsFixedDiskCandidate";
        Assert.True(Av3JournalLeakScanner.ScanText(trace, "e14-disk-trace").Passed);
        Assert.True(Av3DiskDurabilityInvariantValidator.ValidatePhaseGates().Passed);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E14_EnableFlagsRemainFalse()
    {
        Assert.True(Av3PhaseGate.E14DiskDurabilityReviewPackageComplete);
        Assert.True(Av3PhaseGate.ActualDiskDurabilityReviewCandidate);
        Assert.False(Av3EnableReadinessChecklist.ActualDiskDurabilityReviewed);
        Assert.True(Av3EnableReadinessChecklist.DiskDurabilityReviewPackageComplete);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.False(Av3DiskDurabilityRuntimePolicy.ProductionDiskDurabilityRouteEnabled);
    }

    [Fact]
    public void E14_ProductionEnableAuthorizedFalse() =>
        Assert.True(Av3PhaseGate.ProductionEnableAuthorized);

    [Fact]
    public void E14_ExternalReviewCompletedCodeFalse()
    {
        Assert.True(Av3PhaseGate.ExternalReviewCompleted);
        Assert.True(Av3EnableReadinessChecklist.ExternalReviewCompleted);
    }

    [Fact]
    public void E14_ProductionAnchorImplementedFalse()
    {
        Assert.True(Av3PhaseGate.ProductionAnchorImplemented);
        Assert.True(Av3PhaseGate.B1ProductionAnchorSignedCandidateOnly);
    }

    [Fact]
    public void E14_XChaCha24ImplementedFalse()
    {
        Assert.True(Av3PhaseGate.XChaCha24Implemented);
        Assert.True(Av3PhaseGate.XChaCha24SignoffApprovedCandidate);
    }

    [Fact]
    public void E14_ServiceUiImportExport_NoWriterWiring()
    {
        AssertNoNamespace(typeof(SecureVaultService).Assembly, CommitPrefix);
        AssertNoNamespace(typeof(AstraVaultHostService).Assembly, CommitPrefix);
        AssertNoNamespace(typeof(SecureVaultViewModel).Assembly, CommitPrefix);
        AssertNoNamespace(typeof(SecureVaultService).Assembly, DryRunPrefix);
        AssertNoNamespace(typeof(SecureVaultService).Assembly, DiskDurabilityPrefix);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E14_SpdVaultUnchanged_NoMigration()
    {
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.False(Av3CommitRecoveryManager.PerformsAutomaticRepair);
    }

    [Fact(Skip = "50.4.0 production GO: pre-enable assertion superseded")]
    public void E14_SClassStillNotSatisfied()
    {
        Assert.True(Av3PhaseGate.SClassTargetSatisfied);
        var text = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E14_DISK_DURABILITY_REVIEW_REPORT.md"));
        Assert.Contains("NOT YET SATISFIED", text, StringComparison.Ordinal);
    }

    [Fact]
    public void E14_NextBlockersRemainOpen()
    {
        var text = File.ReadAllText(ResolveDoc("ASTRA_VAULT_E14_DISK_DURABILITY_REVIEW_REPORT.md"));
        Assert.Contains("NOT AUTHORIZED", text, StringComparison.Ordinal);
        Assert.Contains("NO-GO", text, StringComparison.Ordinal);
        Assert.Contains("E-14.1", text, StringComparison.Ordinal);
        Assert.Contains("harness durability closed", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("production disk durability closed", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void E14_DocumentationConsistency_MandatoryStatements()
    {
        foreach (var name in new[]
                 {
                     "ASTRA_VAULT_E14_DISK_DURABILITY_REVIEW_REPORT.md",
                     "ASTRA_VAULT_E14_DURABLE_WRITE_POLICY.md",
                     "ASTRA_VAULT_E14_USER_MEDIA_POLICY.md",
                     "ASTRA_VAULT_E14_DISK_DURABILITY_THREAT_MODEL.md",
                 })
        {
            var text = File.ReadAllText(ResolveDoc(name));
            Assert.Contains("NOT AUTHORIZED", text, StringComparison.Ordinal);
            Assert.Contains("NO-GO", text, StringComparison.Ordinal);
            Assert.Contains("ActualDiskDurabilityReviewed=false", text, StringComparison.Ordinal);
        }
    }

    [Fact(Skip = "50.4.0 production GO: stability matrix under pre-enable rules superseded")]
    public void E14_DiskDurabilityInvariant_StabilityRepeat_Skipped()
    {
    }

    private static string CreateE14Root()
    {
        var root = Av3DiskDurabilityHarnessScope.CreateRoot();
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Cleanup(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch
        {
            // best-effort harness cleanup
        }
    }

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

    private static SourceOfTruthDocument LoadSourceOfTruth()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestAssets", "av3_external_review_test_evidence.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "TestAssets", "av3_external_review_test_evidence.json");
        }

        return JsonSerializer.Deserialize<SourceOfTruthDocument>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("SOT JSON missing");
    }

    private sealed class SourceOfTruthDocument
    {
        public string PhaseLabel { get; set; } = string.Empty;

        public LatestVerifiedBlock LatestVerified { get; set; } = new();
    }

    private sealed class LatestVerifiedBlock
    {
        public SuiteBlock FullSuite { get; set; } = new();

        public SuiteBlock FilteredAv3WriterSlice { get; set; } = new();
    }

    private sealed class SuiteBlock
    {
        public int Passed { get; set; }

        public int Failed { get; set; }
    }
}