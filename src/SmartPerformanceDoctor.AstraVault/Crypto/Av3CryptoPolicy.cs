namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>Crypto posture — fail-closed downgrade rules (writer still disabled).</summary>
public static class Av3CryptoPolicy
{
    /// <summary>Read-only unlock may accept transitional ChaCha12 (LOCKED golden vectors).</summary>
    public const bool TransitionalReadAllowsChaCha12 = true;

    /// <summary>Future production write requires XChaCha24 — enforced in downgrade guard (writer off).</summary>
    public const bool ProductionWriteRequiresXChaCha24 = true;

    /// <summary>E-12: TARGET AEAD implemented in code — not the same as production sign-off.</summary>
    public const bool XChaCha24AeadCodePresent = true;

    public static bool AllowsDecrypt(ushort declaredSuiteId, ushort aadSuiteId)
    {
        if (declaredSuiteId != aadSuiteId)
        {
            return false;
        }

        return Av3AeadAlgorithmId.IsKnown(declaredSuiteId);
    }
}