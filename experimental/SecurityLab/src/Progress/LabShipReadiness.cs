using System.Text;
using SmartPerformanceDoctor.SecurityLab.Hardening;
using SmartPerformanceDoctor.SecurityLab.Migration;
using SmartPerformanceDoctor.SecurityLab.ProductBridge;

namespace SmartPerformanceDoctor.SecurityLab.Progress;

/// <summary>
/// Ship-readiness for Lab product path (50.4.0). Installer released after user complete declaration.
/// AV3 final remains separate (ProductionWriter OFF).
/// </summary>
public static class LabShipReadiness
{
    public sealed class Item
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required bool Ready { get; init; }
        public required bool BlocksShip { get; init; }
        public string Note { get; init; } = "";
    }

    public sealed class Report
    {
        public required IReadOnlyList<Item> Items { get; init; }
        public bool LabCoreShipReady { get; init; }
        public bool InstallerReady { get; init; }
        public bool Av3FinalReady { get; init; }
        public string Recommendation { get; init; } = "";

        public string ToHumanSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Lab Ship Readiness ===");
            sb.AppendLine($"LabCoreShipReady={LabCoreShipReady}");
            sb.AppendLine($"InstallerReady={InstallerReady}");
            sb.AppendLine($"Av3FinalReady={Av3FinalReady}");
            sb.AppendLine(Recommendation);
            foreach (var i in Items)
            {
                sb.AppendLine(
                    $"  {(i.Ready ? "OK" : "—")} [{i.Id}] {i.Title}"
                    + (i.BlocksShip ? " *block" : "")
                    + (string.IsNullOrWhiteSpace(i.Note) ? "" : " · " + i.Note));
            }

            return sb.ToString();
        }
    }

    public static Report Evaluate()
    {
        var design = DesignProgressScore.Calculate();
        var self = LabSelfCheckSuite.Run();
        var release = LabReleaseHardeningChecklist.Evaluate();
        var mig = LabToAv3MigrationGate.Evaluate();
        var pkg = LabReleaseState.InstallerPackageReleased;

        var items = new List<Item>
        {
            I("SR1", "Self-check suite pass", self.AllPass, true, $"{self.Passed}/{self.Total}"),
            I("SR2", "Release ship-core", release.ShipCoreReady, true, $"{release.MetShip}/{release.TotalShip}"),
            I("SR3", "AV3 ProductionWriter authorized", Av3GateSnapshot.ProductionWriterEnabled, true, "50.4.0 GO"),
            I("SR4", "Lab→AV3 migrate execute authorized", mig.ExecuteAllowed, true, "re-encrypt path"),
            I("SR5", "Shipping track 100%", design.ShippingTrackPercent >= 99.5, true,
                design.ShippingTrackPercent.ToString("0.#") + "%"),
            I("SR6", "S-class design 100%", design.DesignSClassPercent >= 99.5, true,
                design.DesignSClassPercent.ToString("0.#") + "%"),
            I("SR7", "Installer package released", pkg, true, LabReleaseState.SetupFileName),
            I("SR8", "AV3 external review", Av3GateSnapshot.ExternalReviewCompleted, false,
                "not required for Lab ship GO"),
        };

        var core = items.Where(i => i.BlocksShip).All(i => i.Ready);
        return new Report
        {
            Items = items,
            LabCoreShipReady = core,
            InstallerReady = pkg,
            Av3FinalReady = Av3GateSnapshot.ProductionWriterEnabled && Av3GateSnapshot.ExternalReviewCompleted,
            Recommendation = core && pkg
                ? $"Lab v5 GO · Setup {LabReleaseState.SetupFileName} · AV3 gates authorized (50.4.0)."
                : "Lab 코어/패키지 미준비 — self-check 또는 package 플래그 확인."
        };
    }

    private static Item I(string id, string title, bool ready, bool blocks, string note) =>
        new() { Id = id, Title = title, Ready = ready, BlocksShip = blocks, Note = note };
}
