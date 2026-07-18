namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>E-7/E-9.1 harness commit guard facade — delegates to per-root <see cref="Av3HarnessCommitGuardRegistry"/>.</summary>
internal static class Av3WriterCommitGuard
{
    internal static readonly Av3HarnessCommitGuardRegistry Registry = new();

    public static IDisposable EnterHarnessCommit(string vaultRoot, Guid transactionId) =>
        Registry.AcquireHarnessLease(vaultRoot, transactionId);

    public static void ClearVaultHarnessState(string vaultRoot) =>
        Registry.PurgeRootHarnessState(vaultRoot);

    /// <summary>Diagnostic reset for isolated test diagnostics only — not a substitute for lease cleanup.</summary>
    internal static void DiagnosticResetAllHarnessStateForTests() =>
        Registry.DiagnosticResetAllHarnessStateForTests();
}