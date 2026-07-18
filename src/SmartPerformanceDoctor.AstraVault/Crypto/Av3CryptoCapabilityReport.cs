using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>Non-secret capability summary for gates and docs.</summary>
public static class Av3CryptoCapabilityReport
{
    public static string PublicSummary =>
        "av3_crypto_chacha12_transitional=read;"
        + "av3_crypto_xchacha24_target="
        + (Av3CryptoPolicy.XChaCha24AeadCodePresent ? "code" : "missing")
        + ";av3_crypto_xchacha24_implemented_flag="
        + (Av3PhaseGate.XChaCha24Implemented ? "true" : "false")
        + ";av3_crypto_sclass="
        + (Av3PhaseGate.SClassTargetSatisfied ? "satisfied" : "not_yet");
}