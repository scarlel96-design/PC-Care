namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-11 anchor provider kinds (harness + documented production options).</summary>
public enum Av3AnchorProviderKind
{
    HarnessFileMonotonic = 1,
    LocalTrustedMonotonic = 2,
    DpapiTpmBacked = 3,
    SignedLocalFile = 4,
    ExternalUserControlled = 5,
    RemovableRecovery = 6,
    Unsupported = 7
}