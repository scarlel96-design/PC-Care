using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-13 trusted anchor runtime gates (harness enabled; production route fail-closed).</summary>
public static class Av3TrustedAnchorRuntimePolicy
{
    public const bool HarnessTrustedAnchorEnabled = true;

    public static bool ProductionTrustedAnchorRouteEnabled =>
        Av3PhaseGate.ProductionAnchorImplemented && Av3PhaseGate.ProductionWriterEnabled;

    public const bool StoresPublicDigestsOnly = true;

    public const string TrustedStateFileName = "av3-trusted-anchor.state.json";

    public const string TrustedPendingFileName = "av3-trusted-anchor.pending.json";

    public const string ExternalWitnessStubFileName = "av3-external-witness.stub.json";
}