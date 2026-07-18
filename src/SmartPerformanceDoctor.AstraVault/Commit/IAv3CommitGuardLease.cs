namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>Disposable lease for a single in-flight harness commit on one canonical vault root (E-9.1).</summary>
public interface IAv3CommitGuardLease : IDisposable
{
    string CanonicalVaultRoot { get; }

    Guid TransactionId { get; }
}