namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>Digest-only external witness API contract (stub/mock only in E-13; no live server calls).</summary>
public static class Av3ExternalWitnessStubContract
{
    public const string ApiVersion = "av3-external-witness-v1";

    public sealed class WitnessRequest
    {
        public Guid VaultId { get; init; }

        public ulong ObservedGeneration { get; init; }

        public string HeaderRootDigestHex { get; init; } = string.Empty;

        public string CurrentWitnessDigestHex { get; init; } = string.Empty;
    }

    public sealed class WitnessResponse
    {
        public ulong MonotonicCounter { get; init; }

        public string WitnessDigestHex { get; init; } = string.Empty;

        public string SignatureHex { get; init; } = string.Empty;

        public bool ServerAvailable { get; init; } = true;

        public bool ReplayDetected { get; init; }

        public bool SignatureValid { get; init; } = true;
    }
}