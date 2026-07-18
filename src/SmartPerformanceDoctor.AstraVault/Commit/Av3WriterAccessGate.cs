using SmartPerformanceDoctor.AstraVault.Target;

namespace SmartPerformanceDoctor.AstraVault.Commit;

/// <summary>Enforces disabled production routes; separates harness-only invocation (E-6).</summary>
public static class Av3WriterAccessGate
{
    public const string ErrorProductionDisabled = "av3_writer_production_route_disabled";
    public const string ErrorHarnessOnly = "av3_writer_harness_only";
    public const string ErrorWriterEnableNoGo = "av3_writer_enable_not_ready";
    public const string ErrorJournalProductionDisabled = "av3_journal_production_route_disabled";
    public const string ErrorIsolatedRootRequired = "av3_writer_isolated_root_required";
    public const string ErrorCommitInFlight = "av3_writer_commit_in_flight";
    public const string ErrorReentrantCommit = "av3_writer_reentrant_commit_blocked";
    public const string ErrorDuplicateTransaction = "av3_writer_duplicate_transaction_id";
    public const string ErrorMigrationDisabled = "av3_migration_route_disabled";
    public const string ErrorAnchorProductionDisabled = "av3_anchor_production_route_disabled";

    /// <summary>Legacy token for test path builders; validation uses <see cref="HarnessDirectoryPrefixes"/>.</summary>
    public const string HarnessRootToken = "av3-e";

    private static readonly string[] HarnessDirectoryPrefixes =
    [
        "av3-harness-",
        "av3-e7-",
        "av3-e71-",
        "av3-e8-",
        "av3-e6-",
        "av3-e62-",
        "av3-e3-",
        "av3-e91-",
        "av3-e11-",
        "av3-e13-",
        "av3-e14-",
    ];

    private static readonly string[] ForbiddenUserWorkspaceTokens =
    [
        "Documents",
        "Desktop",
        "Downloads",
    ];

    public static void DenyProductionCreate()
    {
        if (!Av3PhaseGate.ProductionWriterEnabled || !Av3PhaseGate.WriterEnableReady)
        {
            throw new Av3WriterRouteBlockedException(
                !Av3PhaseGate.ProductionWriterEnabled
                    ? ErrorProductionDisabled
                    : ErrorWriterEnableNoGo);
        }
    }

    /// <summary>Fail-closed stack: phase gate + enable readiness (E-7).</summary>
    public static void EnsureProductionRouteFailClosed() => DenyProductionCreate();

    public static void DenyMigrationRoute()
    {
        if (!Av3PhaseGate.MigrationEnabled)
        {
            throw new Av3WriterRouteBlockedException(ErrorMigrationDisabled);
        }
    }

    public static bool IsPublicErrorClassSafe(string? publicErrorClass)
    {
        if (string.IsNullOrWhiteSpace(publicErrorClass))
        {
            return false;
        }

        return publicErrorClass.StartsWith("av3_", StringComparison.Ordinal)
            && !ContainsForbiddenPublicToken(publicErrorClass);
    }

    internal static bool ContainsForbiddenPublicToken(string text)
    {
        string[] forbidden =
        [
            "password", "VMK", "DEK", "SECRET-MARKER", "spd-vault", ":\\", "C:\\Users", ".pdf", ".docx"
        ];
        foreach (var token in forbidden)
        {
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static void EnsureWriterEnableReadyForProduction()
    {
        if (!Av3PhaseGate.WriterEnableReady)
        {
            throw new Av3WriterRouteBlockedException(ErrorWriterEnableNoGo);
        }
    }

    public static void EnsureHarnessRoute(bool testHarnessInvocation, string? vaultRoot)
    {
        if (!testHarnessInvocation)
        {
            throw new Av3WriterRouteBlockedException(ErrorHarnessOnly);
        }

        EnsureIsolatedRoot(vaultRoot);
    }

    public static void EnsureIsolatedRoot(string? vaultRoot)
    {
        if (!TryNormalizeHarnessRoot(vaultRoot, out _))
        {
            throw new Av3WriterRouteBlockedException(ErrorIsolatedRootRequired);
        }
    }

    /// <summary>E-7.1: harness roots must be under OS temp with an approved directory prefix.</summary>
    internal static bool TryNormalizeHarnessRoot(string? vaultRoot, out string normalizedFullPath)
    {
        normalizedFullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(vaultRoot))
        {
            return false;
        }

        if (ContainsUserWorkspacePath(vaultRoot))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(vaultRoot);
        }
        catch
        {
            return false;
        }

        if (ContainsUserWorkspacePath(fullPath))
        {
            return false;
        }

        var tempRoot = Path.GetFullPath(
            Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar);
        if (!fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var segments = fullPath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var hasHarnessPrefix = segments.Any(segment =>
            HarnessDirectoryPrefixes.Any(prefix => segment.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)));
        if (!hasHarnessPrefix)
        {
            return false;
        }

        normalizedFullPath = fullPath;
        return true;
    }

    private static bool ContainsUserWorkspacePath(string path)
    {
        foreach (var token in ForbiddenUserWorkspaceTokens)
        {
            if (path.Contains($"{Path.DirectorySeparatorChar}{token}{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || path.Contains($"{Path.AltDirectorySeparatorChar}{token}{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith($"{Path.DirectorySeparatorChar}{token}", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static void DenyJournalProductionRoute()
    {
        if (!Av3PhaseGate.JournalWriterEnabled)
        {
            throw new Av3WriterRouteBlockedException(ErrorJournalProductionDisabled);
        }
    }

    public static bool IsProductionRouteAllowed =>
        Av3PhaseGate.ProductionWriterEnabled && Av3PhaseGate.WriterEnableReady;
}