namespace SmartPerformanceDoctor.App.Models.Security;

public enum SecureVaultProgressPhase
{
    Preparing,
    Unsealing,
    Restoring,
    RemovingFromVault,
    Adding,
    Sealing,
    Completed
}

public sealed class SecureVaultProgressReport
{
    public SecureVaultProgressPhase Phase { get; init; }
    public int Percent { get; init; }
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public string? CurrentItem { get; init; }
    public int ProcessedCount { get; init; }
    public int TotalCount { get; init; }
}