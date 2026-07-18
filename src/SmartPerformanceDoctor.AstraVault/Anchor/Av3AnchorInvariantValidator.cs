using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.WriterDesign;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-11 anchor invariant checks linked to phase gates.</summary>
public static class Av3AnchorInvariantValidator
{
    public static Av3AnchorInvariantReport ValidatePhaseGates()
    {
        var violations = new List<Av3AnchorInvariantViolation>();

        if (!InvariantExpectProductionAnchorDisabled())
        {
            violations.Add(Code(Av3AnchorInvariant.NoProductionAnchorWhileDisabled, "production_anchor_gate_open"));
        }

        if (!InvariantExpectHarnessOnlyRoute())
        {
            violations.Add(Code(Av3AnchorInvariant.HarnessOnlyAnchorRoute, "anchor_harness_route_open"));
        }

        if (!InvariantExpectProductionEnableUnauthorized())
        {
            violations.Add(Code(Av3AnchorInvariant.NoProductionAnchorWhileDisabled, "production_enable_authorized"));
        }

        if (Av3AnchorDesignPolicy.StoresSecrets)
        {
            violations.Add(Code(Av3AnchorInvariant.NoSecretsInAnchorStore, "anchor_stores_secrets"));
        }

        if (Av3AnchorDesignPolicy.StoresPathsOrFilenamesInAnchorLog)
        {
            violations.Add(Code(Av3AnchorInvariant.NoPathsInAnchorStore, "anchor_stores_paths"));
        }

        if (!InvariantExpectPublicDigestWitnessOnly())
        {
            violations.Add(Code(Av3AnchorInvariant.PublicDigestWitnessOnly, "anchor_not_digest_only"));
        }

        return Report(violations);
    }

    public static Av3AnchorInvariantReport ValidateAnchorPosture(
        bool pipelineCommitted,
        bool anchorUpdated,
        Av3AnchorStatus anchorStatus)
    {
        var violations = new List<Av3AnchorInvariantViolation>();
        violations.AddRange(ValidatePhaseGates().Violations);

        if (pipelineCommitted && !anchorUpdated && anchorStatus != Av3AnchorStatus.AnchorFresh)
        {
            violations.Add(Code(Av3AnchorInvariant.UpdateAfterCommitOnly, "anchor_not_updated_after_commit"));
        }

        if (anchorStatus is Av3AnchorStatus.AnchorRecoveryRequired or Av3AnchorStatus.AnchorMismatch)
        {
            violations.Add(Code(Av3AnchorInvariant.FailClosedOnCorruptAnchor, "anchor_posture_fail_closed"));
        }

        return Report(violations);
    }

    public static Av3AnchorInvariantReport ValidatePublicSurface(string? text, string channel)
    {
        if (Av3AnchorPublicSurface.IsPublicTextSafe(text))
        {
            return Report([]);
        }

        return Report([Code(Av3AnchorInvariant.NoSecretsInAnchorStore, $"anchor_surface_leak_{channel}")]);
    }

    internal static bool InvariantExpectProductionAnchorDisabled() =>
        Av3PhaseGate.ProductionEnableAuthorized
            ? Av3PhaseGate.ProductionAnchorImplemented
              && Av3AnchorDesignPolicy.ProductionAnchorImplemented
            : !Av3PhaseGate.ProductionAnchorImplemented
              && !Av3AnchorDesignPolicy.ProductionAnchorImplemented
              && !Av3AnchorRuntimePolicy.ProductionAnchorRouteEnabled;

    internal static bool InvariantExpectHarnessOnlyRoute() =>
        Av3PhaseGate.E11AnchorHarnessEnabled
        && Av3AnchorRuntimePolicy.HarnessAnchorEnabled
        && (Av3PhaseGate.ProductionEnableAuthorized || !Av3PhaseGate.ProductionAnchorImplemented);

    internal static bool InvariantExpectProductionEnableUnauthorized() =>
        Av3PhaseGate.ProductionEnableAuthorized
            ? Av3PhaseGate.ProductionWriterEnabled && Av3PhaseGate.WriterEnableReady
            : !Av3PhaseGate.ProductionEnableAuthorized
              && !Av3PhaseGate.ProductionWriterEnabled
              && !Av3PhaseGate.WriterEnableReady;

    internal static bool InvariantExpectPublicDigestWitnessOnly() =>
        Av3AnchorRuntimePolicy.StoresPublicDigestsOnly
        && !Av3AnchorDesignPolicy.StoresSecrets
        && !Av3AnchorDesignPolicy.StoresPathsOrFilenamesInAnchorLog;

    private static Av3AnchorInvariantViolation Code(Av3AnchorInvariant invariant, string code) =>
        new() { Invariant = invariant, PublicCode = code };

    private static Av3AnchorInvariantReport Report(List<Av3AnchorInvariantViolation> violations) =>
        new() { Passed = violations.Count == 0, Violations = violations };
}