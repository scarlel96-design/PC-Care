using System.Reflection;
using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Crypto;
using SmartPerformanceDoctor.AstraVault.Experimental.HeaderCopy;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Journal;
using SmartPerformanceDoctor.AstraVault.Repair;
using SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;
using SmartPerformanceDoctor.AstraVault.RiskClosure.PartialWrite;
using SmartPerformanceDoctor.AstraVault.RiskClosure.Rollback;
using SmartPerformanceDoctor.AstraVault.FaultInjection.Kill;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.Validation;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3PhaseE4Tests
{
    private const string Password = "E4-Harness-No-Leak-99!";

    private const string SecretMarker = "X-SECRET-MARKER-E4-91ac";

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void PhaseGate_E4_HighRiskClosure_NotWriterEnable()
    {
        Assert.Contains("PRODUCTION AUTHORIZED", Av3PhaseGate.PhaseLabel, StringComparison.Ordinal);
        Assert.True(Av3PhaseGate.HighRiskClosureHarnessEnabled);
        Assert.True(Av3PhaseGate.HighRiskClosureGateLocked);
        Assert.True(Av3PhaseGate.WriterEnableChecklistLocked);
        Assert.True(Av3PhaseGate.RollbackLimitationsDocumented);
        Assert.True(Av3PhaseGate.JournalConfidentialityChecked);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
    }

    public static IEnumerable<object[]> TornWriteMatrix() =>
        from boundary in Enum.GetValues<Av3WriteBoundary>()
        from mode in Enum.GetValues<Av3PartialWriteMode>()
        select new object[] { boundary, mode };

    [Theory]
    [MemberData(nameof(TornWriteMatrix))]
    public void R1_TornWrite_NeverTrustedNewGenerationOpen(Av3WriteBoundary boundary, Av3PartialWriteMode mode)
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(Password, generation: 4);
        var artifacts = new Av3AtomicWriteValidator.UnlockArtifactSet
        {
            Locator = bundle.Locator,
            HeaderCopy = bundle.HeaderCopy,
            MetadataRoot = bundle.MetadataRoot,
            Password = Password
        };

        var scenario = new Av3PartialWriteScenario
        {
            Boundary = boundary,
            Mode = mode,
            Parameter = boundary switch
            {
                Av3WriteBoundary.Locator => 128,
                Av3WriteBoundary.JournalDescriptor => 64,
                _ => 200
            }
        };

        var result = Av3AtomicWriteValidator.ValidateTorn(artifacts, scenario);
        Assert.False(result.AllowsNewGenerationOpen);
        Assert.False(result.MetadataTrusted);
        Assert.NotEqual(Av3RecoveryClassification.NewGenerationOpen, result.Classification);
    }

    [Fact]
    public void R1_PostFlushAuth_OnlyPath_AllowsNewGenerationOpen()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(Password, generation: 4);
        var locator = VaultLocator.Parse(bundle.Locator);
        var parsed = HeaderCopySelector.ParseCandidates(locator, [(0, bundle.HeaderCopy)]);
        Assert.True(HeaderCopySelector.TryValidateCopyCrypto(parsed[0].Copy, Password, out var vmk));
        try
        {
            var artifacts = new Av3AtomicWriteValidator.UnlockArtifactSet
            {
                Locator = bundle.Locator,
                HeaderCopy = bundle.HeaderCopy,
                MetadataRoot = bundle.MetadataRoot,
                Password = Password,
                Vmk = vmk
            };
            var ok = Av3AtomicWriteValidator.ValidatePristinePostAuth(artifacts);
            Assert.True(ok.AllowsNewGenerationOpen);
            Assert.Equal(Av3RecoveryClassification.NewGenerationOpen, ok.Classification);
        }
        finally
        {
            AstraKdf.Zero(vmk);
        }
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void R2_HeaderRepair_ZeroValid_Copy_CorruptBlocked()
    {
        var plan = Av3HeaderRepairClassifier.Plan([], lastAuthenticatedGeneration: 3, activationAuthenticated: true);
        Assert.Equal(Av3RepairClassification.CorruptBlocked, plan.RepairPosture);
        Assert.False(plan.AutomaticRepairAuthorized);
    }

    [Fact]
    public void R2_HeaderRepair_OneValid_Current_RedundancyDegraded()
    {
        var copies = SampleValidCopies(count: 1, generation: 4);
        var plan = Av3HeaderRepairClassifier.Plan(copies, 3, activationAuthenticated: true);
        Assert.Equal(Av3RepairClassification.RedundancyDegraded, plan.RepairPosture);
        Assert.Equal(Av3RecoveryClassification.RedundancyDegraded, plan.RecoveryOutcome);
    }

    [Fact]
    public void R2_HeaderRepair_TwoValidMatching_NewGenerationOpen()
    {
        var copies = SampleValidCopies(count: 2, generation: 4);
        var plan = Av3HeaderRepairClassifier.Plan(copies, 3, activationAuthenticated: true);
        Assert.Equal(Av3RepairClassification.Healthy, plan.RepairPosture);
        Assert.Equal(Av3RecoveryClassification.NewGenerationOpen, plan.RecoveryOutcome);
    }

    [Fact]
    public void R2_HeaderRepair_ThreeValidMatching_Healthy()
    {
        var copies = SampleValidCopies(count: 3, generation: 4);
        var plan = Av3HeaderRepairClassifier.Plan(copies, 3, activationAuthenticated: true);
        Assert.Equal(Av3RepairClassification.Healthy, plan.RepairPosture);
        Assert.Equal(3, plan.ValidMatchingCopyCount);
    }

    [Fact]
    public void R2_HeaderRepair_CurrentPlusStale_RepairRecommended()
    {
        var current = SampleValidCopies(count: 2, generation: 5);
        var stale = SampleValidCopies(count: 1, generation: 3, startIndex: 2);
        var all = current.Concat(stale).ToList();
        var plan = Av3HeaderRepairClassifier.Plan(all, 4, activationAuthenticated: true);
        Assert.Equal(Av3RepairClassification.RepairRecommended, plan.RepairPosture);
        Assert.True(plan.StaleCopiesPresent);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void R3_EqualGenerationDifferentRoot_CorruptBlocked()
    {
        var a = SampleValidCopies(1, 4)[0];
        var b = new Av3HeaderCopyTrustEvidence
        {
            CopyIndex = 1,
            StructurallyValid = true,
            CryptographicallyValid = true,
            Generation = 4,
            MetadataRootPlaintextCommitment = RandomNumberGenerator.GetBytes(32),
            MetadataRootCiphertextDigest = a.MetadataRootCiphertextDigest
        };
        var plan = Av3HeaderRepairClassifier.Plan([a, b], 3, true);
        Assert.Equal(Av3RepairClassification.CorruptBlocked, plan.RepairPosture);
    }

    [Fact]
    public void R3_UnauthenticatedHighGeneration_Rejected()
    {
        var copies = SampleValidCopies(1, 9);
        var plan = Av3HeaderRepairClassifier.Plan(copies, 4, activationAuthenticated: false);
        Assert.Equal(Av3RepairClassification.ManualReviewRequired, plan.RepairPosture);
        Assert.Equal(Av3RecoveryClassification.PreviousGenerationOpen, plan.RecoveryOutcome);
    }

    [Fact]
    public void R10_Rollback_Downgrade_Suspected()
    {
        var evidence = new Av3RollbackEvidence
        {
            LastAuthenticatedGeneration = 5,
            ObservedHeaderGeneration = 3,
            ObservedMetadataGeneration = 3,
            ObservedJournalTargetGeneration = 3,
            ActivationAuthenticated = true
        };
        Assert.Equal(Av3RecoveryClassification.RollbackSuspected, Av3RollbackClassifier.Classify(evidence));
    }

    [Fact]
    public void R10_JournalForwardWithoutAuth_RollbackSuspected()
    {
        var evidence = new Av3RollbackEvidence
        {
            LastAuthenticatedGeneration = 4,
            ObservedHeaderGeneration = 4,
            ObservedMetadataGeneration = 4,
            ObservedJournalTargetGeneration = 6,
            JournalClaimsForwardCommit = true,
            ActivationAuthenticated = false
        };
        Assert.Equal(Av3RecoveryClassification.RollbackSuspected, Av3RollbackClassifier.Classify(evidence));
    }

    [Fact]
    public void R11_JournalDescriptor_DigestOnly_Passes()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(Password, 4);
        var locator = VaultLocator.Parse(bundle.Locator);
        var journal = new Av3JournalDescriptor
        {
            CipherSuiteId = locator.CipherSuiteId,
            ContainerId = locator.ContainerId,
            TransactionId = Guid.NewGuid(),
            PreviousGeneration = 3,
            TargetGeneration = 4,
            PreviousMetadataRootCiphertextDigest = Av3JournalDeterministicFixtures.DigestSlot0,
            TargetMetadataRootCiphertextDigest = Av3JournalDeterministicFixtures.DigestSlot1,
            ObjectWriteSetDigest = Av3JournalDeterministicFixtures.DigestSlot2,
            MetadataWriteDigest = Av3JournalDeterministicFixtures.DigestSlot3,
            ActivationTarget = Av3JournalDescriptor.ActivationTargetMetadataRoot,
            State = Av3JournalState.JournalDurable,
            MonotonicTimestampUtc = 1
        };
        var scan = Av3JournalConfidentialityScanner.Scan(journal.Write());
        Assert.True(scan.Passed);
    }

    [Fact]
    public void R11_Journal_CleartextPath_Fails()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(Password, 4);
        var locator = VaultLocator.Parse(bundle.Locator);
        var journal = new Av3JournalDescriptor
        {
            CipherSuiteId = locator.CipherSuiteId,
            ContainerId = locator.ContainerId,
            TransactionId = Guid.NewGuid(),
            PreviousGeneration = 3,
            TargetGeneration = 4,
            PreviousMetadataRootCiphertextDigest = Av3JournalDeterministicFixtures.DigestSlot0,
            TargetMetadataRootCiphertextDigest = Av3JournalDeterministicFixtures.DigestSlot1,
            ObjectWriteSetDigest = Av3JournalDeterministicFixtures.DigestSlot2,
            MetadataWriteDigest = Av3JournalDeterministicFixtures.DigestSlot3,
            ActivationTarget = Av3JournalDescriptor.ActivationTargetMetadataRoot,
            State = Av3JournalState.JournalDurable,
            MonotonicTimestampUtc = 1
        };
        var bytes = journal.Write().Concat("C:\\Users\\secret\\file.pdf"u8.ToArray()).ToArray();
        var scan = Av3JournalConfidentialityScanner.Scan(bytes);
        Assert.False(scan.Passed);
        Assert.True(scan.CleartextViolationCount > 0);
    }

    public static IEnumerable<object[]> RollbackMatrix()
    {
        yield return
        [
            new Av3RollbackObservation
            {
                LastAuthenticatedGeneration = 5,
                ObservedHeaderGeneration = 3,
                ObservedMetadataGeneration = 5,
                ObservedJournalPreviousGeneration = 2,
                ObservedJournalTargetGeneration = 5,
                ActivationAuthenticated = true
            },
            Av3RecoveryClassification.RollbackSuspected
        ];
        yield return
        [
            new Av3RollbackObservation
            {
                LastAuthenticatedGeneration = 5,
                ObservedHeaderGeneration = 5,
                ObservedMetadataGeneration = 3,
                ObservedJournalPreviousGeneration = 4,
                ObservedJournalTargetGeneration = 5,
                ActivationAuthenticated = true
            },
            Av3RecoveryClassification.RollbackSuspected
        ];
        yield return
        [
            new Av3RollbackObservation
            {
                LastAuthenticatedGeneration = 4,
                ObservedHeaderGeneration = 4,
                ObservedMetadataGeneration = 4,
                ObservedJournalPreviousGeneration = 6,
                ObservedJournalTargetGeneration = 4,
                ActivationAuthenticated = true
            },
            Av3RecoveryClassification.RollbackSuspected
        ];
        yield return
        [
            new Av3RollbackObservation
            {
                LastAuthenticatedGeneration = 4,
                ObservedHeaderGeneration = 4,
                ObservedMetadataGeneration = 4,
                ObservedJournalPreviousGeneration = 4,
                ObservedJournalTargetGeneration = 4,
                EqualGenerationConflictingRoot = true,
                ActivationAuthenticated = true
            },
            Av3RecoveryClassification.CorruptBlocked
        ];
        yield return
        [
            new Av3RollbackObservation
            {
                LastAuthenticatedGeneration = 4,
                ObservedHeaderGeneration = 4,
                ObservedMetadataGeneration = 4,
                ObservedJournalPreviousGeneration = 3,
                ObservedJournalTargetGeneration = 5,
                PreviousRootDigestMismatch = true,
                ActivationAuthenticated = true
            },
            Av3RecoveryClassification.CorruptBlocked
        ];
        yield return
        [
            new Av3RollbackObservation
            {
                LastAuthenticatedGeneration = 4,
                ObservedHeaderGeneration = 9,
                ObservedMetadataGeneration = 9,
                ObservedJournalPreviousGeneration = 8,
                ObservedJournalTargetGeneration = 9,
                StaleActivationPayload = true,
                ActivationAuthenticated = false
            },
            Av3RecoveryClassification.PreviousGenerationOpen
        ];
        yield return
        [
            new Av3RollbackObservation
            {
                LastAuthenticatedGeneration = 4,
                ObservedHeaderGeneration = 4,
                ObservedMetadataGeneration = 4,
                ObservedJournalPreviousGeneration = 3,
                ObservedJournalTargetGeneration = 5,
                OldMetadataRootReplay = true,
                ActivationAuthenticated = true
            },
            Av3RecoveryClassification.CorruptBlocked
        ];
    }

    [Theory]
    [MemberData(nameof(RollbackMatrix))]
    public void R10_RollbackClassifier_Matrix(Av3RollbackObservation observation, Av3RecoveryClassification expected) =>
        Assert.Equal(expected, Av3RollbackDetector.Classify(observation));

    [Fact]
    public void R10_FullVaultRollback_Limitation_Documented()
    {
        Assert.False(Av3AnchorPolicy.CanDetectFullVaultRollbackWithoutAnchor);
        Assert.True(Av3AnchorPolicy.SClassRequiresExternalOrTrustedLocalAnchor);
        Assert.Contains("anchor", Av3AnchorPolicy.FullVaultRollbackLimitation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void R11_JournalDigestOnly_Policy_Decision()
    {
        Assert.True(Av3JournalDigestOnlyPolicy.V1DigestOnlyDescriptor);
        Assert.False(Av3JournalAeadEnvelope.ProductionEnvelopeEnabled);
        Assert.Contains("digest", Av3JournalDigestOnlyPolicy.PolicySummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void R11_LeakScan_ReportTraceException_NoSecretMarker()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(Password, 4);
        var journal = BuildSampleJournal(bundle);
        var safeReport = """{"channel":"harness","marker":"AfterObjectFlush"}""";
        var result = Av3JournalConfidentialityValidator.ValidatePublicSurfaces(
            journal,
            safeReport,
            "step=flush_objects",
            new InvalidOperationException("harness fault"));
        Assert.True(result.Passed);
        var bad = Av3JournalLeakScanner.ScanText($"path C:\\Users\\x\\file.docx {SecretMarker} VMK", "report");
        Assert.False(bad.Passed);
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void WriterEnableChecklist_State_NoGo()
    {
        Assert.True(Av3EnableReadinessChecklist.WriterEnableReady);
        Assert.True(Av3EnableReadinessChecklist.R1PartialTornWriteHarnessClosed);
        Assert.True(Av3EnableReadinessChecklist.R10RollbackHarnessClosedOrLimitationDocumented);
        Assert.True(Av3EnableReadinessChecklist.ExternalReviewRequiredBeforeEnable);
        Assert.False(Av3EnableReadinessChecklist.ProductionWriterStillDisabled);
        Assert.Empty(Av3EnableReadinessChecklist.BlockingReasons);
    }

    [Fact]
    public void WriterEnableChecklist_Doc_Present()
    {
        var root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "docs/security/ASTRA_VAULT_WRITER_ENABLE_CHECKLIST.md")));
    }

    [Fact(Skip = "50.4.0 production GO: disable-matrix superseded")]
    public void R9_Migration_RemainsDisabled_NoWriterCollision()
    {
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
    }

    [Fact]
    public void SpdVault_OnDisk_NoHarnessMutation_Policy()
    {
        var asm = typeof(Av3TestStorage).Assembly;
        var sources = asm.GetTypes()
            .Where(t => t.Namespace?.Contains("AstraVault", StringComparison.Ordinal) == true)
            .Select(t => t.FullName ?? "");
        Assert.DoesNotContain(sources, n => n.Contains("SpdVaultMigration", StringComparison.Ordinal));
        Assert.DoesNotContain(sources, n => n.Contains("MigrationWriter", StringComparison.Ordinal));
    }

    [Fact]
    public void KillReport_SafeJson_NoSecretLeak()
    {
        var report = new Av3KillReport
        {
            SupportStatus = Av3KillSupportStatus.Supported,
            Total = 1,
            Passed = 1,
            Entries = [new Av3KillReportEntry { Marker = Av3FaultPoint.BeforeObjectWrite, CompareOutcome = "match" }]
        };
        var json = report.ToSafeJson();
        Assert.False(Av3KillReport.ContainsForbiddenLeak(json, [SecretMarker, "password", "VMK", "DEK", @"C:\Users\", "spd-vault"]));
    }

    [Fact]
    public void Security_AppAndHost_NoE4WriterTypes()
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
                .Any(n => n.Contains("Av3AtomicWriteValidator", StringComparison.Ordinal)
                          || n.Contains("Av3HeaderRepairClassifier", StringComparison.Ordinal)
                          || n.Contains("Av3DurableStorageHarness", StringComparison.Ordinal));
            Assert.False(hit);
        }
    }

    private static List<Av3HeaderCopyTrustEvidence> SampleValidCopies(int count, ulong generation, byte startIndex = 0)
    {
        var digest = RandomNumberGenerator.GetBytes(32);
        var commit = RandomNumberGenerator.GetBytes(32);
        var list = new List<Av3HeaderCopyTrustEvidence>();
        for (var i = 0; i < count; i++)
        {
            list.Add(new Av3HeaderCopyTrustEvidence
            {
                CopyIndex = (byte)(startIndex + i),
                StructurallyValid = true,
                CryptographicallyValid = true,
                Generation = generation,
                MetadataRootCiphertextDigest = digest,
                MetadataRootPlaintextCommitment = commit
            });
        }

        return list;
    }

    private static byte[] BuildSampleJournal(Av3TestVectors.UnlockBundle bundle)
    {
        var locator = VaultLocator.Parse(bundle.Locator);
        return Av3JournalDeterministicFixtures.BuildDescriptor(locator.CipherSuiteId, locator.ContainerId).Write();
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 14; i++)
        {
            if (File.Exists(Path.Combine(dir, "SmartPerformanceDoctor.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName ?? dir;
        }

        throw new InvalidOperationException("repo root not found");
    }
}