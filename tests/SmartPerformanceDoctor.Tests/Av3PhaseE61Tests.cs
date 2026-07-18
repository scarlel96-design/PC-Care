using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Journal;
using SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.Validation;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class Av3PhaseE61Tests
{
    private const string Password = "E61-Harness-No-Leak-88!";
    private const string SecretMarker = "X-SECRET-MARKER-E61-4a02";

    [Fact]
    public void PhaseGate_E61_JournalScannerStabilization_GatesUnchanged()
    {
        Assert.Contains("PRODUCTION AUTHORIZED", Av3PhaseGate.PhaseLabel, StringComparison.Ordinal);
        Assert.True(Av3PhaseGate.JournalLeakScannerDeterministic);
        Assert.True(Av3PhaseGate.JournalBinaryScanSeparated);
        Assert.True(Av3PhaseGate.DisabledProductionWriterImplementationPresent);
        Assert.True(Av3PhaseGate.ProductionWriterEnabled);
        Assert.True(Av3PhaseGate.JournalWriterEnabled);
        Assert.True(Av3PhaseGate.MigrationEnabled);
        Assert.True(Av3PhaseGate.WriterEnableReady);
    }

    [Fact]
    public void R11_BinaryDigest_VmKBytes_NoStructuralFalsePositive()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(Password, 4);
        var locator = VaultLocator.Parse(bundle.Locator);
        var journal = new Av3JournalDescriptor
        {
            CipherSuiteId = locator.CipherSuiteId,
            ContainerId = locator.ContainerId,
            TransactionId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            PreviousGeneration = 3,
            TargetGeneration = 4,
            PreviousMetadataRootCiphertextDigest = Av3JournalDeterministicFixtures.DigestSlot0,
            TargetMetadataRootCiphertextDigest = Av3JournalDeterministicFixtures.DigestVmKAsciiTrap,
            ObjectWriteSetDigest = Av3JournalDeterministicFixtures.DigestSlot2,
            MetadataWriteDigest = Av3JournalDeterministicFixtures.DigestSlot3,
            ActivationTarget = Av3JournalDescriptor.ActivationTargetMetadataRoot,
            State = Av3JournalState.JournalDurable,
            MonotonicTimestampUtc = 1
        };
        var scan = Av3JournalConfidentialityScanner.Scan(journal.Write());
        Assert.True(scan.Passed);
        Assert.Equal("digest_only_ok", scan.PublicSummary);
    }

    [Fact]
    public void R11_TextualReport_SecretMarker_TriggersLeak()
    {
        var leak = Av3JournalTextualLeakScanner.ScanText($"safe report {SecretMarker}", "report");
        Assert.False(leak.Passed);
        Assert.True(leak.ViolationCount > 0);
    }

    [Fact]
    public void R11_TextualException_PathMarker_TriggersLeak()
    {
        var leak = Av3JournalLeakScanner.ScanException(
            new InvalidOperationException(@"failed at C:\Users\secret\doc.pdf"),
            "exception");
        Assert.False(leak.Passed);
    }

    [Fact]
    public void R11_JournalFieldAllowlist_DeterministicDescriptor_Passes()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(Password, 4);
        var locator = VaultLocator.Parse(bundle.Locator);
        var bytes = Av3JournalDeterministicFixtures.BuildDescriptor(locator.CipherSuiteId, locator.ContainerId).Write();
        var scan = Av3JournalConfidentialityScanner.Scan(bytes);
        Assert.True(scan.Passed);
        Assert.True(Av3JournalBinaryFieldPolicy.ValidateParsedDescriptor(
            Av3JournalDescriptor.Parse(bytes),
            out _));
    }

    [Fact]
    public void R11_JournalAppendix_ForbiddenPath_Rejected()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(Password, 4);
        var locator = VaultLocator.Parse(bundle.Locator);
        var baseJournal = Av3JournalDeterministicFixtures.BuildDescriptor(locator.CipherSuiteId, locator.ContainerId).Write();
        var withFilename = baseJournal.Concat("report.pdf"u8.ToArray()).ToArray();
        var scan = Av3JournalConfidentialityScanner.Scan(withFilename);
        Assert.False(scan.Passed);
        Assert.Equal("cleartext_appendix_detected", scan.PublicSummary);
    }

    [Fact]
    public void R11_JournalAppendix_ForbiddenPlaintextMetadata_Rejected()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(Password, 4);
        var locator = VaultLocator.Parse(bundle.Locator);
        var baseJournal = Av3JournalDeterministicFixtures.BuildDescriptor(locator.CipherSuiteId, locator.ContainerId).Write();
        var withMeta = baseJournal.Concat("plaintext_metadata=user-notes"u8.ToArray()).ToArray();
        var scan = Av3JournalConfidentialityScanner.Scan(withMeta);
        Assert.False(scan.Passed);
    }

    [Fact]
    public void R11_DeterministicJournalScan_Stable_1000Iterations()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(Password, 4);
        var locator = VaultLocator.Parse(bundle.Locator);
        var bytes = Av3JournalDeterministicFixtures.BuildDescriptor(locator.CipherSuiteId, locator.ContainerId).Write();
        for (var i = 0; i < 1000; i++)
        {
            var scan = Av3JournalConfidentialityScanner.Scan(bytes);
            Assert.True(scan.Passed, $"iteration={i} summary={scan.PublicSummary}");
            var surfaces = Av3JournalConfidentialityValidator.ValidatePublicSurfaces(
                bytes,
                """{"channel":"e61"}""",
                "step=RecordJournal",
                exception: null);
            Assert.True(surfaces.Passed, $"iteration={i}");
        }
    }

    [Fact]
    public void R11_ValidatePublicSurfaces_BinaryUsesStructural_NotTextScanOnDigests()
    {
        var bundle = Av3TestVectors.BuildUnlockBundle(Password, 4);
        var locator = VaultLocator.Parse(bundle.Locator);
        var bytes = new Av3JournalDescriptor
        {
            CipherSuiteId = locator.CipherSuiteId,
            ContainerId = locator.ContainerId,
            TransactionId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            PreviousGeneration = 3,
            TargetGeneration = 4,
            PreviousMetadataRootCiphertextDigest = Av3JournalDeterministicFixtures.DigestSlot0,
            TargetMetadataRootCiphertextDigest = Av3JournalDeterministicFixtures.DigestSlot1,
            ObjectWriteSetDigest = Av3JournalDeterministicFixtures.DigestVmKAsciiTrap,
            MetadataWriteDigest = Av3JournalDeterministicFixtures.DigestSlot3,
            ActivationTarget = Av3JournalDescriptor.ActivationTargetMetadataRoot,
            State = Av3JournalState.JournalDurable,
            MonotonicTimestampUtc = 1
        }.Write();
        var result = Av3JournalConfidentialityValidator.ValidatePublicSurfaces(
            bytes,
            """{"channel":"harness"}""",
            "flush_journal",
            new InvalidOperationException("harness fault"));
        Assert.True(result.Passed);
    }
}