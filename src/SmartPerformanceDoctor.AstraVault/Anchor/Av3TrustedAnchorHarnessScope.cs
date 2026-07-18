using SmartPerformanceDoctor.AstraVault.Commit;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-13 isolated trusted anchor harness scope (av3-e13- temp roots only).</summary>
public static class Av3TrustedAnchorHarnessScope
{
    public const string E13RootPrefix = "av3-e13-";

    public static string CreateRoot() =>
        Path.Combine(Path.GetTempPath(), $"{E13RootPrefix}{Guid.NewGuid():N}");

    public static void Ensure(string? vaultRoot, bool testHarnessInvocation)
    {
        if (!testHarnessInvocation)
        {
            throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorHarnessOnly);
        }

        EnsureE13Root(vaultRoot);
    }

    public static void EnsureE13Root(string? vaultRoot)
    {
        if (!IsE13RootAllowed(vaultRoot, out _))
        {
            throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorIsolatedRootRequired);
        }
    }

    public static bool IsE13RootAllowed(string? vaultRoot, out string normalizedFullPath)
    {
        normalizedFullPath = string.Empty;
        if (!Av3WriterAccessGate.TryNormalizeHarnessRoot(vaultRoot, out var normalized))
        {
            return false;
        }

        var segments = normalized.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        var allowed = segments.Any(s => s.StartsWith(E13RootPrefix, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            return false;
        }

        normalizedFullPath = normalized;
        return true;
    }

    public static string ResolveTrustedStoreDirectory(string vaultRoot)
    {
        EnsureE13Root(vaultRoot);
        var normalized = Path.GetFullPath(vaultRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(normalized)
            ?? throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorIsolatedRootRequired);
        var leaf = Path.GetFileName(normalized);
        return Path.Combine(parent, $"{leaf}-trusted-anchor");
    }
}