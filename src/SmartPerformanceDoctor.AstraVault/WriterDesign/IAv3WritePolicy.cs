namespace SmartPerformanceDoctor.AstraVault.WriterDesign;

/// <summary>
/// Production writer policy gate (design only). Enforces non-goals: no auto-migration, no default origin delete, digest-only journal v1.
/// </summary>
/// <remarks>
/// <para><b>Failure modes:</b> policy violation → commit rejected before durable I/O; no partial enable of cleartext journal or migration.</para>
/// <para><b>Secret handling:</b> policy object holds no keys; implementations must not log password/VMK/DEK/paths.</para>
/// <para><b>Logging policy:</b> container id, generation, public error class only — never policy override reasons that echo user paths.</para>
/// <para><b>Cancellation:</b> N/A on immutable policy snapshot; session coordinator enforces abort before activation.</para>
/// <para><b>Sync/async:</b> synchronous property queries; evaluated at session open and commit boundaries.</para>
/// <para><b>Idempotency:</b> policy version fixed per vault format generation; not per-transaction.</para>
/// <para><b>Testability:</b> injectable fake policy in harness; defaults must match <c>ASTRA_VAULT_PRODUCTION_WRITER_DESIGN.md</c> non-goals.</para>
/// <para><b>Production enable gate:</b> requires <see cref="Target.Av3PhaseGate.ProductionWriterEnabled"/> and <see cref="Target.Av3PhaseGate.ExternalReviewCompleted"/> — both false in E-5/E-5.1.</para>
/// </remarks>
public interface IAv3WritePolicy
{
    bool AllowsUserOriginDeletionByDefault { get; }

    bool AllowsLegacyMigration { get; }

    bool AllowsCleartextJournalFields { get; }

    bool RequiresPostFlushAuthentication { get; }

    bool RequiresTrustedAnchorForSClassRollback { get; }
}