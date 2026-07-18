namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>Production writer route blocked while gates are false (E-6).</summary>
public sealed class Av3WriterRouteBlockedException : InvalidOperationException
{
    public Av3WriterRouteBlockedException(string publicErrorClass)
        : base(publicErrorClass)
    {
        PublicErrorClass = publicErrorClass;
    }

    public string PublicErrorClass { get; }
}