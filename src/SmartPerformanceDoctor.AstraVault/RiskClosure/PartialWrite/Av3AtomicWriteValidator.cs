using System.Security.Cryptography;
using SmartPerformanceDoctor.AstraVault.Experimental;
using SmartPerformanceDoctor.AstraVault.FaultInjection;
using SmartPerformanceDoctor.AstraVault.Format;
using SmartPerformanceDoctor.AstraVault.Journal;
using SmartPerformanceDoctor.AstraVault.Validation;

namespace SmartPerformanceDoctor.AstraVault.RiskClosure.PartialWrite;

/// <summary>R1 closure: torn/partial writes must not yield trusted new-generation open.</summary>
public static class Av3AtomicWriteValidator
{
    public sealed class UnlockArtifactSet
    {
        public byte[] Locator { get; init; } = [];
        public byte[] HeaderCopy { get; init; } = [];
        public byte[] MetadataRoot { get; init; } = [];
        public string Password { get; init; } = "";
        public byte[]? Vmk { get; init; }
    }

    public static Av3AtomicWriteValidationResult ValidateTorn(
        UnlockArtifactSet artifacts,
        Av3PartialWriteScenario scenario)
    {
        var pristine = SelectPristine(artifacts, scenario.Boundary);
        var torn = Av3TornWriteSimulator.Apply(pristine, scenario);
        return ClassifyTorn(artifacts, scenario, torn);
    }

    public static Av3AtomicWriteValidationResult ValidatePristinePostAuth(UnlockArtifactSet artifacts)
    {
        if (artifacts.Vmk is null || artifacts.Vmk.Length == 0)
        {
            return Fail(Av3WriteBoundary.HeaderCopy, Av3PartialWriteMode.Truncation, Av3RecoveryClassification.CorruptBlocked, "vmk_missing");
        }

        var auth = Av3HarnessPostCommitAuthenticator.AuthenticateFullChain(
            artifacts.HeaderCopy,
            artifacts.MetadataRoot,
            artifacts.Vmk);
        if (!auth.Success)
        {
            return Fail(Av3WriteBoundary.HeaderCopy, Av3PartialWriteMode.Truncation, Av3RecoveryClassification.CorruptBlocked, "post_auth_failed");
        }

        return new Av3AtomicWriteValidationResult
        {
            Boundary = Av3WriteBoundary.HeaderCopy,
            Mode = Av3PartialWriteMode.Truncation,
            Classification = Av3RecoveryClassification.NewGenerationOpen,
            MetadataTrusted = true,
            AllowsNewGenerationOpen = true,
            PublicReason = "post_flush_reread_aead_ok"
        };
    }

    private static byte[] SelectPristine(UnlockArtifactSet artifacts, Av3WriteBoundary boundary) =>
        boundary switch
        {
            Av3WriteBoundary.Locator => artifacts.Locator,
            Av3WriteBoundary.HeaderCopy or Av3WriteBoundary.ActivationPayload => artifacts.HeaderCopy,
            Av3WriteBoundary.MetadataRootCiphertext => artifacts.MetadataRoot,
            Av3WriteBoundary.JournalDescriptor => BuildMinimalJournal(artifacts),
            Av3WriteBoundary.ObjectPlaceholder => RandomNumberGenerator.GetBytes(64),
            _ => []
        };

    private static byte[] BuildMinimalJournal(UnlockArtifactSet artifacts)
    {
        var locator = VaultLocator.Parse(artifacts.Locator);
        var journal = new Av3JournalDescriptor
        {
            CipherSuiteId = locator.CipherSuiteId,
            ContainerId = locator.ContainerId,
            TransactionId = Guid.NewGuid(),
            PreviousGeneration = 1,
            TargetGeneration = 2,
            PreviousMetadataRootCiphertextDigest = RandomNumberGenerator.GetBytes(32),
            TargetMetadataRootCiphertextDigest = RandomNumberGenerator.GetBytes(32),
            ObjectWriteSetDigest = RandomNumberGenerator.GetBytes(32),
            MetadataWriteDigest = RandomNumberGenerator.GetBytes(32),
            ActivationTarget = Av3JournalDescriptor.ActivationTargetMetadataRoot,
            State = Av3JournalState.JournalDurable,
            MonotonicTimestampUtc = 1
        };
        return journal.Write();
    }

    private static Av3AtomicWriteValidationResult ClassifyTorn(
        UnlockArtifactSet artifacts,
        Av3PartialWriteScenario scenario,
        byte[] torn)
    {
        try
        {
            switch (scenario.Boundary)
            {
                case Av3WriteBoundary.Locator:
                    VaultLocator.Parse(torn);
                    return Fail(scenario, Av3RecoveryClassification.RecoveryRequired, "locator_parse_unexpected");
                case Av3WriteBoundary.JournalDescriptor:
                    Av3JournalDescriptor.Parse(torn);
                    return Fail(scenario, Av3RecoveryClassification.RecoveryRequired, "journal_parse_unexpected");
                case Av3WriteBoundary.ObjectPlaceholder:
                    return Fail(scenario, Av3RecoveryClassification.PreviousGenerationOpen, "object_partial");
                case Av3WriteBoundary.MetadataRootCiphertext:
                    MetadataRootCiphertextEnvelope.Parse(torn);
                    return TryUnlockWithTornMetadata(artifacts, scenario, torn);
                case Av3WriteBoundary.HeaderCopy:
                case Av3WriteBoundary.ActivationPayload:
                    return TryUnlockWithTornHeader(artifacts, scenario, torn);
                default:
                    return Fail(scenario, Av3RecoveryClassification.UnknownFailClosed, "boundary_unknown");
            }
        }
        catch (UnlockValidationException)
        {
            return Fail(scenario, Av3RecoveryClassification.CorruptBlocked, "unlock_fail_closed");
        }
        catch (CryptographicException)
        {
            return Fail(scenario, Av3RecoveryClassification.CorruptBlocked, "parse_fail_closed");
        }
        catch (Exception)
        {
            return Fail(scenario, Av3RecoveryClassification.CorruptBlocked, "fail_closed");
        }
    }

    private static Av3AtomicWriteValidationResult TryUnlockWithTornHeader(
        UnlockArtifactSet artifacts,
        Av3PartialWriteScenario scenario,
        byte[] tornHeader)
    {
        try
        {
            ReadOnlyUnlockValidator.Validate(
                artifacts.Locator,
                [(0, tornHeader)],
                artifacts.MetadataRoot,
                artifacts.Password);
            return Fail(scenario, Av3RecoveryClassification.CorruptBlocked, "torn_header_must_not_unlock");
        }
        catch (UnlockValidationException)
        {
            return Fail(scenario, Av3RecoveryClassification.CorruptBlocked, "header_torn_rejected");
        }
    }

    private static Av3AtomicWriteValidationResult TryUnlockWithTornMetadata(
        UnlockArtifactSet artifacts,
        Av3PartialWriteScenario scenario,
        byte[] tornMetadata)
    {
        try
        {
            ReadOnlyUnlockValidator.Validate(
                artifacts.Locator,
                [(0, artifacts.HeaderCopy)],
                tornMetadata,
                artifacts.Password);
            return Fail(scenario, Av3RecoveryClassification.CorruptBlocked, "torn_metadata_must_not_unlock");
        }
        catch (UnlockValidationException)
        {
            return Fail(scenario, Av3RecoveryClassification.CorruptBlocked, "metadata_torn_rejected");
        }
    }

    private static Av3AtomicWriteValidationResult Fail(
        Av3PartialWriteScenario scenario,
        Av3RecoveryClassification classification,
        string reason) =>
        Fail(scenario.Boundary, scenario.Mode, classification, reason);

    private static Av3AtomicWriteValidationResult Fail(
        Av3WriteBoundary boundary,
        Av3PartialWriteMode mode,
        Av3RecoveryClassification classification,
        string reason) =>
        new()
        {
            Boundary = boundary,
            Mode = mode,
            Classification = classification,
            MetadataTrusted = false,
            AllowsNewGenerationOpen = Av3RecoveryClassifier.TrustsMetadata(classification),
            PublicReason = reason
        };
}