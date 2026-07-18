using SmartPerformanceDoctor.AstraVault.Validation;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>Mixed-algorithm and downgrade rejection (fail-closed).</summary>
public static class Av3CryptoDowngradeGuard
{
    public static void EnsureDecryptAllowed(ushort headerSuiteId, ushort payloadSuiteId, bool xchacha24RequiredPolicy)
    {
        if (headerSuiteId == 0 || payloadSuiteId == 0)
        {
            throw new UnlockValidationException();
        }

        if (headerSuiteId != payloadSuiteId)
        {
            throw new UnlockValidationException();
        }

        if (!Av3CryptoPolicy.AllowsDecrypt(headerSuiteId, payloadSuiteId))
        {
            throw new UnlockValidationException();
        }

        if (xchacha24RequiredPolicy && Av3AeadAlgorithmId.IsTransitionalChaCha12(headerSuiteId))
        {
            throw new UnlockValidationException();
        }
    }

    public static bool IsDowngradeAttempt(ushort declaredSuiteId, ushort aadBoundSuiteId) =>
        declaredSuiteId != aadBoundSuiteId
        || (!Av3AeadAlgorithmId.IsKnown(declaredSuiteId));
}