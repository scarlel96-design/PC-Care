using SmartPerformanceDoctor.AstraVault.Anchor;
using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>E-14 disk durability invariants linked to writer/anchor gates.</summary>
public static class Av3DiskDurabilityInvariantValidator
{
    public static Av3DiskDurabilityInvariantReport ValidatePhaseGates()
    {
#pragma warning disable CS0162
        var violations = new List<Av3DiskDurabilityInvariantViolation>();

        if (!Av3DiskDurabilityPolicy.HarnessDurabilityClosedIsNotProductionDiskClosed)
        {
            violations.Add(Code(Av3DiskDurabilityInvariant.DiskDurabilityRequiredForWriterEnable, "harness_prod_disk_conflated"));
        }

        // 50.4.0 GO: durability reviewed + writer gates authorized are expected together.
        if (Av3EnableReadinessChecklist.ActualDiskDurabilityReviewed && !Av3PhaseGate.ProductionEnableAuthorized)
        {
            violations.Add(Code(Av3DiskDurabilityInvariant.ActualDiskDurabilityNotMarkedReviewedWithoutSignoff, "disk_durability_reviewed_without_signoff"));
        }

        if (!Av3DiskDurabilityPolicy.UnknownFilesystemFailClosed)
        {
            violations.Add(Code(Av3DiskDurabilityInvariant.UnsupportedFilesystemNoProductionWriter, "unknown_fs_not_fail_closed"));
        }

        if (!Av3PhaseGate.ProductionEnableAuthorized)
        {
            if (Av3PhaseGate.ProductionWriterEnabled || Av3PhaseGate.WriterEnableReady)
            {
                violations.Add(Code(Av3DiskDurabilityInvariant.DiskDurabilityRequiredForWriterEnable, "writer_enable_gate_open"));
            }

            if (Av3PhaseGate.MigrationEnabled)
            {
                violations.Add(Code(Av3DiskDurabilityInvariant.DiskDurabilityRequiredForWriterEnable, "migration_enabled"));
            }

            if (Av3PhaseGate.ProductionAnchorImplemented)
            {
                violations.Add(Code(Av3DiskDurabilityInvariant.DiskDurabilityRequiredForWriterEnable, "production_anchor_implemented"));
            }

            if (Av3PhaseGate.XChaCha24Implemented)
            {
                violations.Add(Code(Av3DiskDurabilityInvariant.DiskDurabilityRequiredForWriterEnable, "xchacha24_implemented"));
            }
        }
        else if (!Av3EnableReadinessChecklist.ActualDiskDurabilityReviewed)
        {
            violations.Add(Code(Av3DiskDurabilityInvariant.DiskDurabilityRequiredForWriterEnable, "authorized_without_disk_review"));
        }

        if (Av3DiskDurabilityRuntimePolicy.ProductionDiskDurabilityRouteEnabled
            && !Av3PhaseGate.ProductionEnableAuthorized)
        {
            violations.Add(Code(Av3DiskDurabilityInvariant.DiskDurabilityRequiredForWriterEnable, "disk_production_route_enabled"));
        }

        var writerGates = Av3WriterInvariantValidator.ValidateDisabledProductionGates();
        if (!writerGates.Passed)
        {
            violations.Add(Code(Av3DiskDurabilityInvariant.DiskDurabilityRequiredForWriterEnable, "writer_gates_failed"));
        }

        var trustedGates = Av3TrustedAnchorInvariantValidator.ValidatePhaseGates();
        if (!trustedGates.Passed)
        {
            violations.Add(Code(Av3DiskDurabilityInvariant.DiskDurabilityRequiredForWriterEnable, "trusted_anchor_gates_failed"));
        }

        return Report(violations);
#pragma warning restore CS0162
    }

    public static Av3DiskDurabilityInvariantReport ValidatePublicSurface(string? text, string channel)
    {
        if (Av3DiskDurabilityPublicSurface.IsPublicTextSafe(text))
        {
            return Report([]);
        }

        return Report([Code(Av3DiskDurabilityInvariant.DiskDurabilityPublicErrorSafe, $"disk_surface_leak_{channel}")]);
    }

    private static Av3DiskDurabilityInvariantViolation Code(Av3DiskDurabilityInvariant invariant, string code) =>
        new() { Invariant = invariant, PublicCode = code };

    private static Av3DiskDurabilityInvariantReport Report(List<Av3DiskDurabilityInvariantViolation> violations) =>
        new() { Passed = violations.Count == 0, Violations = violations };
}