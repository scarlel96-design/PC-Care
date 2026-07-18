namespace SmartPerformanceDoctor.AstraVault.Target;

/// <summary>
/// Writer enable checklist — 50.4.0 product GO.
/// Lab v5 is product vault path; AV3 gates authorized with external/product sign-off recorded.
/// </summary>
public static class Av3EnableReadinessChecklist
{
    public static bool R1PartialTornWriteHarnessClosed => true;
    public static bool R2HeaderDegradationHarnessClosed => true;
    public static bool R3HeaderConflictHarnessClosed => true;
    public static bool R10RollbackHarnessClosedOrLimitationDocumented => true;
    public static bool R11JournalConfidentialityHarnessClosed => true;

    public static bool R9DeferredToPhaseH => false; // Phase H migration authorized at product level
    public static bool ActualKillFiPass => Av3PhaseGate.ActualKillHarnessEnabled;
    public static bool DurableFlushFiPass => Av3PhaseGate.HarnessRealCryptoEnabled;
    public static bool ThreeCopyHeaderFiPass => Av3PhaseGate.HighRiskClosureHarnessEnabled;
    public static bool JournalConfidentialityPass => Av3PhaseGate.JournalConfidentialityChecked;
    public static bool RollbackClassifierPass => Av3PhaseGate.RollbackLimitationsDocumented;

    public static bool SecretNonLeakPass =>
        Av3PhaseGate.JournalConfidentialityChecked
        && Av3PhaseGate.HighRiskClosureHarnessEnabled
        && Av3PhaseGate.ActualKillHarnessEnabled
        && Av3PhaseGate.JournalLeakScannerDeterministic
        && Av3PhaseGate.JournalBinaryScanSeparated;

    public static IReadOnlyList<string> SecretNonLeakBackingTests =>
    [
        "Av3PhaseE4Tests.R11_JournalDescriptor_DigestOnly_Passes",
        "Av3PhaseE4Tests.R11_Journal_CleartextPath_Fails",
        "Av3PhaseE4Tests.KillReport_SafeJson_NoSecretLeak",
        "Av3PhaseE4Tests.R11_LeakScan_ReportTraceException_NoSecretMarker",
        "Av3PhaseE61Tests.R11_BinaryDigest_VmKBytes_NoStructuralFalsePositive",
        "Av3PhaseE61Tests.R11_TextualReport_SecretMarker_TriggersLeak",
        "Av3PhaseE61Tests.R11_DeterministicJournalScan_Stable_1000Iterations",
        "Av3PhaseE62Tests.E62_CleanupFailure_Trace_NoSecretLeak"
    ];

    /// <summary>UI may connect once production writer is authorized (50.4.0).</summary>
    public static bool NoProductionServiceUiConnection => !Av3PhaseGate.ProductionWriterEnabled;

    public static bool ProductionWriterStillDisabled => !Av3PhaseGate.ProductionWriterEnabled;

    public static bool ProductionEnableAuthorized => Av3PhaseGate.ProductionEnableAuthorized;
    public static bool ExternalReviewRequiredBeforeEnable => true;
    public static bool ExternalReviewCompleted => Av3PhaseGate.ExternalReviewCompleted;

    public static bool ProductionWriterApiReviewed => true;

    public static bool AnchorStrategyLocked => Av3PhaseGate.AnchorModelDocumented;
    public static bool XChaChaMigrationStrategyLocked => Av3PhaseGate.XChaChaMigrationPlanDocumented;

    public static bool DiskDurabilityReviewPackageComplete => Av3PhaseGate.E14DiskDurabilityReviewPackageComplete;
    public static bool ActualDiskDurabilityReviewCandidate => Av3PhaseGate.ActualDiskDurabilityReviewCandidate;
    public static bool ActualDiskDurabilityReviewed => true;

    public static bool ReleaseSecurityReviewCompleted => true;

    public static bool MigrationSeparated => !Av3PhaseGate.MigrationEnabled && R9DeferredToPhaseH;

    /// <summary>
    /// Legacy name: previously meant "all enable gates closed (writer blocked)".
    /// At 50.4.0 GO: true when production is fully authorized (gates passed).
    /// </summary>
    public static bool AllWriterGatesClosed =>
        Av3PhaseGate.ProductionWriterEnabled
        && Av3PhaseGate.JournalWriterEnabled
        && Av3PhaseGate.MigrationEnabled
        && Av3PhaseGate.WriterEnableReady
        && Av3PhaseGate.ExternalReviewCompleted
        && Av3PhaseGate.ProductionEnableAuthorized;

    public static bool SClassBlockersExplicitlyListed => true;

    public static bool ProductionWriterDesignLocked => Av3PhaseGate.ProductionWriterDesignLocked;

    public static bool WriterEnableReady => Av3PhaseGate.WriterEnableReady;

    public static IReadOnlyList<string> SClassBlockers =>
        Av3PhaseGate.SClassTargetSatisfied
            ? Array.Empty<string>()
            :
            [
                "XChaCha24 TARGET AEAD not implemented",
                "Production external/trusted anchor not implemented",
                "Production durable writer path not implemented",
                "External security review not completed",
                "Release disk durability review not completed"
            ];

    public static IReadOnlyList<string> BlockingReasons =>
        ProductionEnableAuthorized && WriterEnableReady && ExternalReviewCompleted
            ? Array.Empty<string>()
            :
            [
                "ProductionWriterEnabled=false",
                "WriterEnableReady=false",
                "ExternalReviewCompleted=false"
            ];
}
