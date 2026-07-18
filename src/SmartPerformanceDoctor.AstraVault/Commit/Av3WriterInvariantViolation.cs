namespace SmartPerformanceDoctor.AstraVault.Commit;

public sealed class Av3WriterInvariantViolation
{
    public Av3WriterInvariant Invariant { get; init; }

    public string PublicCode { get; init; } = "";
}