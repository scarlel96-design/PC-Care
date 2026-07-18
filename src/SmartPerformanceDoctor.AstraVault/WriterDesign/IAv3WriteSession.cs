namespace SmartPerformanceDoctor.AstraVault.WriterDesign;

/// <summary>
/// Unlocked writer session (design). Inputs: container id, authenticated generation, in-memory key material.
/// Outputs: child coordinators. Failures: session expired, lock lost, policy violation.
/// Secrets: keys only in protected memory; never serialized. Logging: container id + generation.
/// Async disposal zeroizes. Cancellation: ends session without commit. Idempotency: session-scoped.
/// Testability: mock session for harness. Production gate: no App/SecureVaultService wiring in E-5.
/// </summary>
public interface IAv3WriteSession : IAsyncDisposable
{
    Guid SessionId { get; }

    ulong AuthenticatedGeneration { get; }

    IAv3TransactionCoordinator Coordinator { get; }

    IAv3WritePolicy Policy { get; }
}