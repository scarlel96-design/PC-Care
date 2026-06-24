namespace SmartPerformanceDoctor.App.Models.Commercial;

public sealed class DiagnosticSignal
{
    public string SignalId { get; init; } = "";
    public string Area { get; init; } = "";
    public string Category { get; init; } = "";
    public string Source { get; init; } = "";
    public string Severity { get; init; } = "info";
    public float Confidence { get; init; }
    public string Evidence { get; init; } = "";
    public string RawValue { get; init; } = "";
    public string NormalizedValue { get; init; } = "";
    public string RecommendedNextProbe { get; init; } = "";
}

public sealed class CommercialRule
{
    public string RuleId { get; init; } = "";
    public string Area { get; init; } = "";
    public string Category { get; init; } = "";
    public string Severity { get; init; } = "";
    public string Risk { get; init; } = "";
    public string ProtocolId { get; init; } = "";
    public string UserMessage { get; init; } = "";
    public float ConfidenceBase { get; init; }
}

public sealed class ResponseProtocol
{
    public string ProtocolId { get; init; } = "";
    public string Area { get; init; } = "";
    public string Risk { get; init; } = "";
    public bool RequiresElevation { get; init; }
    public bool RequiresTarget { get; init; }
}

public sealed class SecureDeleteTarget
{
    public string Path { get; init; } = "";
    public string Type { get; init; } = "file";
    public long Size { get; init; }
    public string StorageType { get; init; } = "Unknown";
    public string FileSystem { get; init; } = "Unknown";
    public string Risk { get; init; } = "review";
    public string RecommendedProtocol { get; init; } = "";
    public int OverwritePasses { get; init; }
    public bool Blocked { get; init; }
    public string BlockReason { get; init; } = "";
}

public sealed class SecureDeletePlan
{
    public string OperationId { get; init; } = "";
    public string Mode { get; init; } = "dry-run";
    public string SecurityLevel { get; init; } = "Professional";
    public IReadOnlyList<SecureDeleteTarget> Targets { get; init; } = Array.Empty<SecureDeleteTarget>();
    public IReadOnlyList<SecureDeleteTarget> BlockedTargets { get; init; } = Array.Empty<SecureDeleteTarget>();
    public int RecoveryResistanceLevel { get; init; }
    public int TechnicalDeletionIntensity { get; init; }
    public bool Level5Certified { get; init; }
    public string CertifiedResistanceLabel { get; init; } = "";
    public string ResistanceDisclaimer { get; init; } = "";
    public string ProfessionalRecoveryRisk { get; init; } = "";
    public string Limitations { get; init; } = "";
    public string EstimatedDuration { get; init; } = "";
    public string ChainSummary { get; init; } = "";
}

public sealed class IntelligenceCenterSnapshot
{
    public int RuleCount { get; init; }
    public int ProtocolCount { get; init; }
    public string PackVersion { get; init; } = "";
    public string Summary { get; init; } = "";
    public IReadOnlyList<string> TopInsights { get; init; } = Array.Empty<string>();
}