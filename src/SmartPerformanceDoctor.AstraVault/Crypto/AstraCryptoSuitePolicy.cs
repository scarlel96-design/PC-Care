namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>CURRENT (implemented) vs TARGET (S-Class) crypto suite policy.</summary>
public static class AstraCryptoSuitePolicy
{
    public const string CurrentPhaseLabel = "CURRENT — BELOW S-CLASS transitional suite";
    public const string TargetPhaseLabel = "TARGET — NOT YET SATISFIED";

    /// <summary>
    /// Implemented today: .NET <see cref="ChaCha20Poly1305"/> with 12-byte nonce (suite id 1).
    /// Labeled XChaCha20Poly1305 historically; does not provide XChaCha20 24-byte extended nonce.
    /// </summary>
    public const string CurrentAeadDescription =
        "ChaCha20-Poly1305 (12-byte nonce, suite_id=1) — BELOW S-CLASS vs VeraCrypt-class target";

    /// <summary>S-Class target: XChaCha20-Poly1305 with 24-byte nonce (E-12 code present; sign-off pending).</summary>
    public const string TargetAeadDescription =
        "XChaCha20-Poly1305 (24-byte nonce, suite_id=3) — E-12 candidate; XChaCha24Implemented=false until E-12.1";
}