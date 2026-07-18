using System.Text;
using SmartPerformanceDoctor.SecurityLab.Migration;
using SmartPerformanceDoctor.SecurityLab.ProductBridge;
using SmartPerformanceDoctor.SecurityLab.VaultV4;

namespace SmartPerformanceDoctor.SecurityLab.Hardening;

/// <summary>
/// S-class unified self-check: pure crypto/policy matrices + optional on-disk container probe.
/// Safe for CI / CLI; does not enable AV3 writer or build installer.
/// </summary>
public static class LabSelfCheckSuite
{
    public sealed class Section
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required bool Pass { get; init; }
        public string Detail { get; init; } = "";
    }

    public sealed class Report
    {
        public required IReadOnlyList<Section> Sections { get; init; }
        public int Passed => Sections.Count(s => s.Pass);
        public int Total => Sections.Count;
        public bool AllPass => Sections.All(s => s.Pass);
        public bool Av3WriterAuthorized => Av3GateSnapshot.ProductionWriterEnabled;
        public bool PackageDeferred => !LabReleaseState.InstallerPackageReleased;

        public string ToHumanSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Lab Self-Check Suite ===");
            sb.AppendLine($"Result {Passed}/{Total} · AllPass={AllPass}");
            sb.AppendLine(
                $"AV3.ProductionWriter={Av3GateSnapshot.ProductionWriterEnabled} · PackageDeferred={PackageDeferred} · "
                + LabReleaseState.Summary);
            foreach (var s in Sections)
            {
                sb.AppendLine($"  {(s.Pass ? "OK" : "FAIL")} [{s.Id}] {s.Title}"
                              + (string.IsNullOrWhiteSpace(s.Detail) ? "" : " · " + s.Detail));
            }

            return sb.ToString();
        }
    }

    /// <summary>Pure checks (no vault password). Optional vaultRoot adds container probe.</summary>
    public static Report Run(string? vaultRoot = null)
    {
        var sections = new List<Section>();

        var aad = LabAadBoundary.Run();
        sections.Add(S("SC1", "LabAadBoundary", aad.AllPass, $"{aad.Passed}/{aad.Total}"));

        var labels = LabSecurityStateLabels.CoversAllEnumValues();
        sections.Add(S("SC2", "SecurityStateLabels", labels, labels ? "all enum" : "missing labels"));

        var release = LabReleaseHardeningChecklist.Evaluate();
        sections.Add(S("SC3", "ReleaseHardening ship-core", release.ShipCoreReady,
            $"{release.MetShip}/{release.TotalShip} PackageAllowed={release.PackageAllowed}"));

        var mig = LabToAv3MigrationGate.Evaluate();
        sections.Add(S("SC4", "Lab→AV3 migrate execute authorized", mig.ExecuteAllowed && mig.DryRunAllowed,
            $"blocking={mig.BlockingCount} dryRun={mig.DryRunAllowed}"));

        var writeOk = LabWriteGate.Evaluate(true, true).Allowed;
        var writeRo = !LabWriteGate.Evaluate(true, false).Allowed;
        sections.Add(S("SC5", "LabWriteGate RO deny", writeOk && writeRo, "write+RO boundary"));

        sections.Add(S("SC6", "AV3 ProductionWriter authorized", Av3GateSnapshot.ProductionWriterEnabled,
            Av3GateSnapshot.StatusSummary));

        var policyLine = LabSessionPolicy.FormatCountdown(
            TimeSpan.FromMinutes(5), TimeSpan.FromHours(1), false, true);
        sections.Add(S("SC7", "Session countdown format", policyLine.Contains("유휴", StringComparison.Ordinal),
            policyLine));

        if (!string.IsNullOrWhiteSpace(vaultRoot) && Directory.Exists(vaultRoot))
        {
            var probe = LabContainerProbe.Probe(vaultRoot);
            sections.Add(S("SC8", "ContainerProbe", probe.Healthy || !probe.LooksLikeLabVault,
                probe.LooksLikeLabVault
                    ? $"{probe.Passed}/{probe.Total} format={probe.FormatId}"
                    : "path not a lab vault (skipped soft)"));
        }
        else
        {
            sections.Add(S("SC8", "ContainerProbe", true, "no --vault (skipped)"));
        }

        return new Report { Sections = sections };
    }

    private static Section S(string id, string title, bool pass, string detail) =>
        new() { Id = id, Title = title, Pass = pass, Detail = detail };
}
