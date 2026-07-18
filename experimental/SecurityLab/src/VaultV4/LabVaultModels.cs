namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

public enum LabKdfProfile
{
    /// <summary>Fast lab/tests only (not for production recommendations).</summary>
    LabFast = 0,
    Balanced = 1,
    Strong = 2,
    Extreme = 3
}

public sealed record LabKdfParams(int Iterations, int MemoryKb, int Parallelism)
{
    public static LabKdfParams FromProfile(LabKdfProfile profile) => profile switch
    {
        LabKdfProfile.LabFast => new(1, 8 * 1024, 1),
        LabKdfProfile.Balanced => new(2, 65_536, 4),
        LabKdfProfile.Extreme => new(4, 262_144, 4),
        _ => new(3, 131_072, 4) // Strong
    };

    public string DisplayName => $"Argon2id ({Iterations}t · {MemoryKb / 1024}MB · p{Parallelism})";
}

public sealed class LabVaultCreateResult
{
    public required bool Success { get; init; }
    public string Message { get; init; } = "";
    public string VaultId { get; init; } = "";
    public string Path { get; init; } = "";
    public IReadOnlyList<string> RecoveryCodes { get; init; } = Array.Empty<string>();
    public string Format { get; init; } = "spd-vault-v4-lab";
    public string KdfProfile { get; init; } = "";
}

public sealed class LabVaultEntry
{
    public required string EntryId { get; init; }
    public required string ObjectId { get; init; }
    public required string DisplayName { get; init; }
    public required string RelativePath { get; init; }
    public long Size { get; init; }
    public string ContentSha256 { get; init; } = "";
    public string AddedAt { get; init; } = "";
}

public sealed class LabVaultOperationResult
{
    public required bool Success { get; init; }
    public string Message { get; init; } = "";
    public string? EntryId { get; init; }
    public int ProcessedCount { get; init; }
    public bool ReadOnly { get; init; }
    public string SecurityState { get; init; } = "";
    public IReadOnlyList<string>? RecoveryCodes { get; init; }
}

public enum LabSecurityState
{
    Locked,
    Unlocking,
    Unlocked,
    ReadOnlyUnlocked,
    Importing,
    Verifying,
    Committing,
    CorruptionDetected,
    RecoveryAvailable,
    AutoLockScheduled,
    SessionExpired
}
