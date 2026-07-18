using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>E-6 default write policy — production disabled, harness-only execution.</summary>
public sealed class Av3DefaultWritePolicy : IAv3WritePolicy
{
    public bool AllowsUserOriginDeletionByDefault => false;

    public bool AllowsLegacyMigration => Av3PhaseGate.MigrationEnabled;

    public bool AllowsCleartextJournalFields => false;

    public bool RequiresPostFlushAuthentication => true;

    public bool RequiresTrustedAnchorForSClassRollback => true;

    public static Av3DefaultWritePolicy Instance { get; } = new();

    /// <summary>E-7: asserts writer/migration gates remain disabled for production routes.</summary>
    public static void EnforceDisabledWriterGates()
    {
        if (!Av3WriterInvariantValidator.InvariantExpectWriterGatesClosed())
        {
            Av3WriterAccessGate.DenyProductionCreate();
        }
    }
}