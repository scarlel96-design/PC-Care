using SmartPerformanceDoctor.AstraVault.Commit;

namespace SmartPerformanceDoctor.AstraVault.Anchor;

/// <summary>E-11 isolated anchor harness scope (av3-e11- temp roots only).</summary>
public static class Av3AnchorHarnessScope
{
    public const string E11RootPrefix = "av3-e11-";

    public static string CreateRoot() =>
        Path.Combine(Path.GetTempPath(), $"{E11RootPrefix}{Guid.NewGuid():N}");

    public static void Ensure(string? vaultRoot, bool testHarnessInvocation)
    {
        if (!testHarnessInvocation)
        {
            throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorHarnessOnly);
        }

        EnsureE11Root(vaultRoot);
    }

    public static void EnsureE11Root(string? vaultRoot)
    {
        if (!IsE11RootAllowed(vaultRoot, out _))
        {
            throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorIsolatedRootRequired);
        }
    }

    public static bool IsE11RootAllowed(string? vaultRoot, out string normalizedFullPath)
    {
        normalizedFullPath = string.Empty;
        if (!Av3WriterAccessGate.TryNormalizeHarnessRoot(vaultRoot, out var normalized))
        {
            return false;
        }

        var segments = normalized.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        var allowed = segments.Any(s => s.StartsWith(E11RootPrefix, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            return false;
        }

        normalizedFullPath = normalized;
        return true;
    }

    /// <summary>Anchor store sits outside the mutable vault tree (sibling directory).</summary>
    public static string ResolveAnchorStoreDirectory(string vaultRoot)
    {
        EnsureE11Root(vaultRoot);
        var normalized = Path.GetFullPath(vaultRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(normalized)
            ?? throw new Av3WriterRouteBlockedException(Av3WriterAccessGate.ErrorIsolatedRootRequired);
        var leaf = Path.GetFileName(normalized);
        return Path.Combine(parent, $"{leaf}-anchor");
    }
}