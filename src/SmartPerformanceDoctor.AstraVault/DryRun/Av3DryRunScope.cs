using SmartPerformanceDoctor.AstraVault.Commit;

namespace SmartPerformanceDoctor.AstraVault.DryRun;

/// <summary>E-8 isolated dry-run scope (stricter than general harness).</summary>
public static class Av3DryRunScope
{
    public const string E8RootPrefix = "av3-e8-";

    public const string HarnessRootPrefix = "av3-harness-";

    public static string CreateRoot() =>
        Path.Combine(Path.GetTempPath(), $"{E8RootPrefix}{Guid.NewGuid():N}");

    public static void Ensure(Av3DryRunOptions options)
    {
        if (!options.TestHarnessInvocation)
        {
            throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorHarnessOnly);
        }

        EnsureDryRunRoot(options.VaultRoot);
        Av3DefaultWritePolicy.EnforceDisabledWriterGates();
    }

    public static void EnsureDryRunRoot(string? vaultRoot)
    {
        if (!IsDryRunRootAllowed(vaultRoot, out _))
        {
            throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorIsolatedRootRequired);
        }
    }

    public static bool IsDryRunRootAllowed(string? vaultRoot, out string normalizedFullPath)
    {
        normalizedFullPath = string.Empty;
        if (!Av3WriterAccessGate.TryNormalizeHarnessRoot(vaultRoot, out var normalized))
        {
            return false;
        }

        var segments = normalized.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var allowed = segments.Any(s =>
            s.StartsWith(E8RootPrefix, StringComparison.OrdinalIgnoreCase)
            || s.StartsWith(HarnessRootPrefix, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            return false;
        }

        normalizedFullPath = normalized;
        return true;
    }
}