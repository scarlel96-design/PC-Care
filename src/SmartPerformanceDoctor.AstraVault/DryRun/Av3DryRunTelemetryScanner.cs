using SmartPerformanceDoctor.AstraVault.Commit;
using SmartPerformanceDoctor.AstraVault.RiskClosure.Journal;

namespace SmartPerformanceDoctor.AstraVault.DryRun;

public sealed class Av3DryRunTelemetryScanResult
{
    public bool Passed { get; init; }

    public string PublicSummary { get; init; } = "telemetry_ok";
}

/// <summary>Scans dry-run surfaces for forbidden tokens (E-8).</summary>
public static class Av3DryRunTelemetryScanner
{
    private static readonly string[] ForbiddenTokens =
    [
        "password",
        "VMK",
        "DEK",
        "SECRET-MARKER",
        "Documents",
        "Desktop",
        "Downloads",
        ".pdf",
        ".docx",
        "spd-vault",
        "C:\\Users",
    ];

    public static Av3DryRunTelemetryScanResult ScanDryRunSurfaces(
        Av3DryRunReport report,
        Av3CommitPipelineRunner.Av3CommitPipelineResult? pipeline)
    {
        var surfaces = new List<string>
        {
            report.ToPublicSummary(),
            report.Manifest.ToPublicJson(),
            report.TraceSummary,
            report.CancellationSummary,
            report.InvariantSummary,
            report.RecoverySummary,
            report.PublicErrorClass
        };

        if (pipeline is not null)
        {
            surfaces.Add(pipeline.Trace.ToPublicSummary());
            surfaces.Add(pipeline.Cancellation?.ToPublicSummary() ?? string.Empty);
            surfaces.Add(pipeline.Classification.ToString());
            surfaces.Add(pipeline.Repair.ToString());
        }

        foreach (var surface in surfaces)
        {
            if (!Av3WriterInvariantValidator.ValidatePublicTextSurface(surface, "dry_run_telemetry").Passed)
            {
                return Fail("invariant_leak_scan");
            }

            if (ContainsForbidden(surface))
            {
                return Fail("forbidden_token");
            }
        }

        return new Av3DryRunTelemetryScanResult { Passed = true };
    }

    public static bool ContainsForbidden(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        foreach (var token in ForbiddenTokens)
        {
            if (text.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return Av3WriterAccessGate.ContainsForbiddenPublicToken(text);
    }

    private static Av3DryRunTelemetryScanResult Fail(string code) =>
        new() { Passed = false, PublicSummary = $"telemetry_fail_{code}" };
}