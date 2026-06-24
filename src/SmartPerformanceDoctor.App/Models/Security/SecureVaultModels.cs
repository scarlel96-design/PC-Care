namespace SmartPerformanceDoctor.App.Models.Security;

public enum SecureVaultState
{
    NotCreated,
    Locked,
    Unlocked
}

public enum SecureVaultEntryKind
{
    StandaloneFile,
    FolderRoot,
    FolderMember,
    LegacyFolderFile
}

public enum SecureVaultBrowsableKind
{
    File,
    SubFolder,
    FolderRoot,
    StandaloneFile
}

public sealed class SecureVaultEntry
{
    public string EntryId { get; init; } = "";
    public string DisplayLabel { get; init; } = "";
    public string ShardName { get; init; } = "";
    public long OriginalSize { get; init; }
    public DateTimeOffset AddedAt { get; init; }
    public bool IsFolderBundle { get; init; }
    public SecureVaultEntryKind Kind { get; init; }
    public string? BundleId { get; init; }
    public string? RelativePath { get; init; }
    public string? OriginalPath { get; init; }
    public bool IsSealedAtOrigin { get; init; }
    public int BlobFormat { get; init; } = 1;
}

public sealed class SecureVaultBrowsableItem
{
    public string Key { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public SecureVaultBrowsableKind Kind { get; init; }
    public string? EntryId { get; init; }
    public string? BundleId { get; init; }
    public string RelativePrefix { get; init; } = "";
    public int ItemCount { get; init; }
    public long TotalSize { get; init; }
    public string? OriginalPath { get; init; }
    public bool IsSealedAtOrigin { get; init; }
    public string IconGlyph { get; init; } = "📄";
    public string DetailLine { get; init; } = "";
    public string TitleLine => string.IsNullOrWhiteSpace(IconGlyph)
        ? DisplayName
        : $"{IconGlyph} {DisplayName}";
    public bool IsNavigable => Kind is SecureVaultBrowsableKind.SubFolder or SecureVaultBrowsableKind.FolderRoot;
}

public sealed class SecureVaultOperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public int ProcessedCount { get; init; }
    public string? RecoveryKey { get; init; }
}

public enum SecureVaultIntegrityIssueKind
{
    MissingShard,
    CorruptShard,
    ContentHashMismatch,
    MissingRedundantCopy,
    OrphanShard,
    InvalidPath,
    InvisibleMember,
    ManifestIntegrity,
    AuditChain
}

public sealed class SecureVaultIntegrityIssue
{
    public SecureVaultIntegrityIssueKind Kind { get; init; }
    public string EntryId { get; init; } = "";
    public string Label { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool Repairable { get; init; }
}

public sealed class SecureVaultIntegrityResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public int CheckedEntries { get; init; }
    public int FailedEntries { get; init; }
    public int RepairedEntries { get; init; }
    public bool ManifestIntegrityValid { get; init; }
    public bool AuditChainValid { get; init; }
    public IReadOnlyList<SecureVaultIntegrityIssue> Issues { get; init; } = Array.Empty<SecureVaultIntegrityIssue>();
    public SecureVaultStorageDiagnostic? StorageDiagnostic { get; init; }
}

public sealed class SecureVaultStorageDiagnostic
{
    public int ShardsOnDisk { get; init; }
    public int ManifestShardReferences { get; init; }
    public int VisibleRootItems { get; init; }
    public int OrphanShardCount { get; init; }
    public int MissingShardCount { get; init; }
    public int InvisibleMemberCount { get; init; }
    public int InvalidRelativePathCount { get; init; }
    public IReadOnlyList<string> OrphanShardNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> MissingShardNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> InvalidRelativePathSamples { get; init; } = Array.Empty<string>();
}

public sealed class SecureVaultSecurityStatus
{
    public string KdfAlgorithm { get; init; } = "";
    public int KdfIterations { get; init; }
    public bool AclHardened { get; init; }
    public bool RecoveryKeyConfigured { get; init; }
    public bool AuditChainValid { get; init; }
    public int AuditEntryCount { get; init; }
    public int RateLimitFailures { get; init; }
    public string CryptoStack { get; init; } = "";
    public string PolicyLine { get; init; } = "";
}

