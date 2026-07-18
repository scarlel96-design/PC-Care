namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Digest-only trusted anchor witness (no secrets, paths, or filenames).</summary>
public sealed class Av3TrustedAnchorWitness
{
    public Guid VaultId { get; init; }

    public Guid AnchorId { get; init; }

    public ulong Generation { get; init; }

    public ulong MonotonicCounter { get; init; }

    public string PreviousWitnessDigestHex { get; init; } = string.Empty;

    public string CurrentWitnessDigestHex { get; init; } = string.Empty;

    public string HeaderRootDigestHex { get; init; } = string.Empty;

    public string MetadataCiphertextDigestHex { get; init; } = string.Empty;

    public string ActivationDigestHex { get; init; } = string.Empty;

    public Av3TrustedAnchorProviderKind ProviderKind { get; init; }

    public Av3TrustedAnchorBindingState MachineBindingState { get; init; }

    public Av3TrustedAnchorExternalState ExternalWitnessState { get; init; }

    public Av3TrustedAnchorOfflineState OfflineGraceState { get; init; }

    public Av3TrustedAnchorRecoveryState RecoveryState { get; init; }
}

public enum Av3TrustedAnchorBindingState
{
    Unknown = 0,
    Bound = 1,
    Mismatch = 2,
    Unavailable = 3,
    RecoveryRequired = 4
}

public enum Av3TrustedAnchorExternalState
{
    Unknown = 0,
    Synchronized = 1,
    Stale = 2,
    RollbackSuspected = 3,
    Unavailable = 4,
    ReplayRejected = 5,
    SignatureInvalid = 6
}

public enum Av3TrustedAnchorOfflineState
{
    Online = 0,
    OfflineGraceReadOnly = 1,
    OfflineRecoveryRequired = 2
}

public enum Av3TrustedAnchorRecoveryState
{
    None = 0,
    RecoveryRequired = 1,
    AccountRecoveryRequired = 2
}