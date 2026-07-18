namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-11 anchor runtime policy constants (not <see cref="RiskClosure.Rollback.Av3AnchorPolicy"/>).</summary>
public static class Av3AnchorRuntimePolicy
{
    public const bool HarnessAnchorEnabled = true;

    public const bool ProductionAnchorRouteEnabled = false;

    public const bool StoresPublicDigestsOnly = true;

    public static bool StoresSecrets => false;

    public static bool StoresPathsOrFilenames => false;

    public const bool UpdateAfterPostAuthCommitOnly = true;

    public const bool FailClosedOnCorrupt = true;

    public const bool SClassRequiresTrustedAnchor = true;

    public const string StateFileName = "av3-anchor.state.json";

    public const string PendingFileName = "av3-anchor.pending.json";

    public const string ErrorAnchorUpdateInFlight = "av3_anchor_update_in_flight";

    public const string ErrorReentrantAnchorUpdate = "av3_anchor_reentrant_update_blocked";

    public const string ErrorDuplicateAnchorUpdate = "av3_anchor_duplicate_update_id";
}