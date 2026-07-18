namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-11 anchor invariant identifiers (harness closure package).</summary>
public enum Av3AnchorInvariant
{
    NoProductionAnchorWhileDisabled = 1,
    HarnessOnlyAnchorRoute = 2,
    NoSecretsInAnchorStore = 3,
    NoPathsInAnchorStore = 4,
    UpdateAfterCommitOnly = 5,
    PublicDigestWitnessOnly = 6,
    FailClosedOnCorruptAnchor = 7
}