using SmartPerformanceDoctor.AstraVault.Commit;

namespace SmartPerformanceDoctor.AstraVault.DiskDurability;

/// <summary>E-14 isolated disk durability harness (av3-e14- temp roots only).</summary>
public static class Av3DiskDurabilityHarnessScope
{
    public const string E14RootPrefix = "av3-e14-";

    public static string CreateRoot() =>
        Path.Combine(Path.GetTempPath(), $"{E14RootPrefix}{Guid.NewGuid():N}");

    public static void Ensure(string? vaultRoot, bool testHarnessInvocation)
    {
        if (!testHarnessInvocation)
        {
            throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorHarnessOnly);
        }

        EnsureE14Root(vaultRoot);
    }

    public static void EnsureE14Root(string? vaultRoot)
    {
        if (!IsE14RootAllowed(vaultRoot, out _))
        {
            throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorIsolatedRootRequired);
        }
    }

    public static bool IsE14RootAllowed(string? vaultRoot, out string normalizedFullPath)
    {
        normalizedFullPath = string.Empty;
        if (!Av3WriterAccessGate.TryNormalizeHarnessRoot(vaultRoot, out var normalized))
        {
            return false;
        }

        var segments = normalized.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (!segments.Any(s => s.StartsWith(E14RootPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        normalizedFullPath = normalized;
        return true;
    }
}