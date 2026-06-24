namespace SmartPerformanceDoctor.Aegis;

public sealed class AegisRecoveryManifestEntry
{
    public string Path { get; set; } = "";
    public string Tier { get; set; } = "app-critical";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public bool SignatureRequired { get; set; }
}

public sealed class AegisRecoveryManifest
{
    public string Product { get; set; } = AegisProduct.Product;
    public string Version { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public List<AegisRecoveryManifestEntry> Files { get; set; } = new();
    public string CapsuleHash { get; set; } = "";
    public string CapsuleVersion { get; set; } = "1";
}

public sealed record AegisMirrorStatus
{
    public bool ManifestReady { get; init; }
    public bool ManifestSignatureValid { get; init; }
    public bool CapsuleReady { get; init; }
    public bool CapsuleHashValid { get; init; }
    public bool AuditChainValid { get; init; }
    public int ProtectedFileCount { get; init; }
    public int IntegrityFailures { get; init; }
    public int RepairedFiles { get; init; }
    public bool RepairAttempted { get; init; }
    public DateTimeOffset? LastCheckAt { get; init; }
    public DateTimeOffset? LastRepairAt { get; init; }
    public string Message { get; init; } = "";
    public IReadOnlyList<string> Findings { get; init; } = Array.Empty<string>();
    public int ProtectionLevel { get; init; }
    public string? RecoveryReportPath { get; init; }
    public bool RecoveryServiceInstalled { get; init; }
    public bool RecoveryServiceRunning { get; init; }
    public bool TpmAvailable { get; init; }
    public string KeyProtectionMode { get; init; } = "";
    public bool OfflineCapsuleReady { get; init; }
    public bool BackupSlotReady { get; init; }
    public string ManifestSource { get; init; } = "primary";
    public bool SafeModeActive { get; init; }
    public string SafeModeReason { get; init; } = "";
}

public sealed class AegisIntegrityFinding
{
    public string RelativePath { get; init; } = "";
    public string Tier { get; init; } = "";
    public string Reason { get; init; } = "";
    public bool Repairable { get; init; }
}