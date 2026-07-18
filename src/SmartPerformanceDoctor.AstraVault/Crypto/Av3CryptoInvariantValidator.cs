using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.Target;
using SmartPerformanceDoctor.AstraVault.Validation;

namespace SmartPerformanceDoctor.AstraVault.Crypto;

/// <summary>E-12 crypto invariants linked to writer/anchor gates.</summary>
public static class Av3CryptoInvariantValidator
{
    public static Av3CryptoInvariantReport ValidatePhaseGates()
    {
        var violations = new List<Av3CryptoInvariantViolation>();

        if (!ExpectXChaCha24CodePresent())
        {
            violations.Add(Code(Av3CryptoInvariant.XChaCha24VectorAuthPassed, "xchacha24_code_missing"));
        }

        if (ExpectXChaCha24ImplementedWithoutPackage())
        {
            violations.Add(Code(Av3CryptoInvariant.XChaCha24NotMarkedImplementedWithoutSignoff, "implemented_without_package"));
        }

        // 50.4.0 ProductionEnableAuthorized: XChaCha24 + production enable flags are expected true.
        if (ExpectXChaCha24ImplementedFlagTrue() && !Av3PhaseGate.ProductionEnableAuthorized)
        {
            violations.Add(Code(Av3CryptoInvariant.XChaCha24NotMarkedImplementedWithoutSignoff, "implemented_flag_true"));
        }

        if (ExpectAnyProductionEnableGateOpen() && !Av3PhaseGate.ProductionEnableAuthorized)
        {
            violations.Add(Code(Av3CryptoInvariant.NoProductionRouteWhileDisabled, "enable_gate_open"));
        }

        if (ExpectProductionAnchorImplemented() && !Av3PhaseGate.ProductionEnableAuthorized)
        {
            violations.Add(Code(Av3CryptoInvariant.ProductionAnchorRemainsFalse, "production_anchor_true"));
        }

        if (!Av3WriterInvariantValidator.ValidateDisabledProductionGates().Passed)
        {
            violations.Add(Code(Av3CryptoInvariant.NoProductionRouteWhileDisabled, "writer_invariant_failed"));
        }

        if (!ExpectCryptoPublicErrorSafe())
        {
            violations.Add(Code(Av3CryptoInvariant.CryptoPublicErrorSafe, "unlock_message_changed"));
        }

        return Report(violations);
    }

    private static bool ExpectXChaCha24CodePresent() => Av3CryptoPolicy.XChaCha24AeadCodePresent;

    private static bool ExpectXChaCha24ImplementedWithoutPackage() =>
        Av3PhaseGate.XChaCha24Implemented && !Av3PhaseGate.E12XChaCha24ClosurePackageComplete;

    private static bool ExpectXChaCha24ImplementedFlagTrue() => Av3PhaseGate.XChaCha24Implemented;

    private static bool ExpectAnyProductionEnableGateOpen() =>
        Av3PhaseGate.ProductionWriterEnabled
        || Av3PhaseGate.JournalWriterEnabled
        || Av3PhaseGate.MigrationEnabled
        || Av3PhaseGate.WriterEnableReady
        || Av3PhaseGate.ExternalReviewCompleted
        || Av3PhaseGate.ProductionEnableAuthorized;

    private static bool ExpectProductionAnchorImplemented() => Av3PhaseGate.ProductionAnchorImplemented;

    private static bool ExpectCryptoPublicErrorSafe() =>
        UnlockValidationException.PublicMessage.StartsWith("Unlock", StringComparison.Ordinal);

    public static Av3CryptoInvariantReport ValidateVectorRoundTrip(Av3AeadVector vector)
    {
        var violations = new List<Av3CryptoInvariantViolation>();
        try
        {
            Av3AeadVectorVerifier.VerifyDecryptPass(vector);
            if (vector.Aad.Length < 2)
            {
                violations.Add(Code(Av3CryptoInvariant.CryptoAlgorithmIdAuthenticated, "aad_too_short"));
            }
        }
        catch (Exception)
        {
            violations.Add(Code(Av3CryptoInvariant.XChaCha24VectorAuthPassed, "vector_failed"));
        }

        return Report(violations);
    }

    public static Av3CryptoInvariantReport ValidateDowngradeRejected(ushort headerSuite, ushort payloadSuite)
    {
        var violations = new List<Av3CryptoInvariantViolation>();
        try
        {
            Av3CryptoDowngradeGuard.EnsureDecryptAllowed(headerSuite, payloadSuite, xchacha24RequiredPolicy: true);
            if (headerSuite != payloadSuite)
            {
                violations.Add(Code(Av3CryptoInvariant.MixedAlgorithmChainRejected, "mixed_not_rejected"));
            }
            else if (Av3AeadAlgorithmId.IsTransitionalChaCha12(headerSuite))
            {
                violations.Add(Code(Av3CryptoInvariant.XChaCha24RequiredPolicyRejectsChaCha12, "chacha12_not_rejected"));
            }
        }
        catch (UnlockValidationException)
        {
            // expected for bad/mixed/downgrade
        }

        return Report(violations);
    }

    private static Av3CryptoInvariantViolation Code(Av3CryptoInvariant id, string detail) =>
        new(id, detail);

    private static Av3CryptoInvariantReport Report(List<Av3CryptoInvariantViolation> violations) =>
        new(violations.Count == 0, violations);
}

public enum Av3CryptoInvariant
{
    CryptoAlgorithmIdAuthenticated = 1,
    XChaCha24RequiredPolicyRejectsChaCha12 = 2,
    DowngradeAttemptRejected = 3,
    MixedAlgorithmChainRejected = 4,
    XChaCha24VectorAuthPassed = 5,
    XChaCha24TamperRejected = 6,
    CryptoPublicErrorSafe = 7,
    XChaCha24NotMarkedImplementedWithoutSignoff = 8,
    NoProductionRouteWhileDisabled = 9,
    ProductionAnchorRemainsFalse = 10
}

public sealed record Av3CryptoInvariantViolation(Av3CryptoInvariant Invariant, string Detail);

public sealed record Av3CryptoInvariantReport(bool Passed, IReadOnlyList<Av3CryptoInvariantViolation> Violations);