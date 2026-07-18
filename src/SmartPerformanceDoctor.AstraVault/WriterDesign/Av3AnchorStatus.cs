namespace SmartPerformanceDoctor.AstraVault.WriterDesign;

/// <summary>Design-only anchor evaluation outcomes (Phase E-5). No production anchor I/O.</summary>
public enum Av3AnchorStatus
{
    AnchorUnavailable,
    AnchorMismatch,
    AnchorRollbackSuspected,
    AnchorFresh,
    AnchorStale,
    AnchorUnsupported,
    AnchorDisabledByUser,
    AnchorRecoveryRequired
}