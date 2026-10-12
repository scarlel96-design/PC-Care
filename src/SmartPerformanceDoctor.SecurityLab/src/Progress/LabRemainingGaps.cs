using System.Text;
using SmartPerformanceDoctor.SecurityLab.ProductBridge;

namespace SmartPerformanceDoctor.SecurityLab.Progress;

/// <summary>
/// Report current production approval gaps. Historical package flags are not approval evidence.
/// </summary>
public static class LabRemainingGaps
{
    public sealed class Gap
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required bool BlockingFullGo { get; init; }
        public string Note { get; init; } = "";
    }

    public sealed class Report
    {
        public required IReadOnlyList<Gap> Gaps { get; init; }
        public int BlockingCount => Gaps.Count(g => g.BlockingFullGo);
        public bool LabCodeComplete { get; init; }
        public string Summary { get; init; } = "";

        public string ToHumanSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Lab Remaining Gaps (honest) ===");
            sb.AppendLine($"LabCodeComplete={LabCodeComplete}");
            sb.AppendLine($"Blocking Lab GO: {BlockingCount}");
            sb.AppendLine(Summary);
            foreach (var g in Gaps)
            {
                sb.AppendLine(
                    $"  {(g.BlockingFullGo ? "HOLD" : "OK")} [{g.Id}] {g.Title}"
                    + (string.IsNullOrWhiteSpace(g.Note) ? "" : " · " + g.Note));
            }

            return sb.ToString();
        }
    }

    public static Report Evaluate()
    {
        var design = DesignProgressScore.Calculate();
        var ship = LabShipReadiness.Evaluate();
        var pkg = LabReleaseState.InstallerPackageReleased;
        var gaps = new List<Gap>
        {
            new()
            {
                Id = "G1",
                Title = "Installer / Setup package",
                BlockingFullGo = !pkg,
                Note = pkg ? "released: " + LabReleaseState.SetupFileName : "missing"
            },
            new()
            {
                Id = "G2",
                Title = "AV3 ProductionWriter enable",
                BlockingFullGo = !Av3GateSnapshot.ProductionWriterEnabled,
                Note = Av3GateSnapshot.StatusSummary
            },
            new()
            {
                Id = "G3",
                Title = "AV3 external review + named sign-off",
                BlockingFullGo = !Av3GateSnapshot.ExternalReviewCompleted,
                Note = "External review remains incomplete"
            },
            new()
            {
                Id = "G4",
                Title = "Lab→AV3 migration authorization",
                BlockingFullGo = !Av3GateSnapshot.MigrationToAv3Enabled,
                Note = "Production migration remains disabled"
            },
            new()
            {
                Id = "G5",
                Title = "Absolute security marketing claims",
                BlockingFullGo = false,
                Note = "Independent security certification has not been completed"
            },
            new()
            {
                Id = "G6",
                Title = "Lab design track 100%",
                BlockingFullGo = design.DesignSClassPercent < 99.5,
                Note = "design=" + design.DesignSClassPercent.ToString("0.#") + "%"
            },
        };

        return new Report
        {
            Gaps = gaps,
            LabCodeComplete = ship.LabCoreShipReady && pkg && design.OverallPercent >= 99.5
                              && Av3GateSnapshot.ProductionWriterEnabled,
            Summary = "Phase 1 NO-GO. AV3 production approval remains closed."
        };
    }
}
