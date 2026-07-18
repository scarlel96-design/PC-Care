namespace SmartPerformanceDoctor.AstraVault.Anchor;

public interface IAv3AnchorGuardLease : IDisposable
{
    string CanonicalVaultRoot { get; }

    Guid UpdateId { get; }
}