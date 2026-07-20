using System.Text;

namespace SmartPerformanceDoctor.SecurityLab.ProductBridge;

/// <summary>
/// Lab mirror of AV3 enable readiness — 50.4.0 product GO.
/// </summary>
public static class Av3LabEnableChecklist
{
    public sealed class Item
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required bool Met { get; init; }
        public required bool RequiredForEnable { get; init; }
        public string Note { get; init; } = "";
    }

    public sealed class Report
    {
        public required IReadOnlyList<Item> Items { get; init; }
        public bool ProductionWriterStillOff => !Av3GateSnapshot.ProductionWriterEnabled;
        public bool EnableAuthorized =>
            Av3GateSnapshot.ProductionWriterEnabled
            && Av3GateSnapshot.WriterEnableReady
            && Av3GateSnapshot.ExternalReviewCompleted;
        public int MetRequired => Items.Count(i => i.RequiredForEnable && i.Met);
        public int TotalRequired => Items.Count(i => i.RequiredForEnable);
        public bool AllRequiredMet => Items.Where(i => i.RequiredForEnable).All(i => i.Met);

        public string ToHumanSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== AV3 Lab Enable Checklist (50.4.0 GO) ===");
            sb.AppendLine($"ProductionWriterEnabled={Av3GateSnapshot.ProductionWriterEnabled}");
            sb.AppendLine($"EnableAuthorized={EnableAuthorized}");
            sb.AppendLine($"Required met: {MetRequired}/{TotalRequired}");
            foreach (var i in Items)
            {
                sb.AppendLine(
                    $"  {(i.Met ? "OK" : "—")} [{i.Id}] {i.Title}"
                    + (i.RequiredForEnable ? " *req" : "")
                    + (string.IsNullOrWhiteSpace(i.Note) ? "" : " · " + i.Note));
            }

            return sb.ToString();
        }
    }

    public static Report Evaluate()
    {
        var items = new List<Item>
        {
            I("L1", "Lab v5 durable path active", true, false, "product track = SecurityLab"),
            I("L2", "Lab FI matrix + kill recovery", true, false, "LabFaultMatrix"),
            I("L3", "UI state labels mapped", true, false, "LabSecurityStateLabels"),
            I("L4", "AV3 ProductionWriter authorized", Av3GateSnapshot.ProductionWriterEnabled, true, "50.4.0 GO"),
            I("L5", "WriterEnableReady", Av3GateSnapshot.WriterEnableReady, true, "true"),
            I("L6", "ExternalReviewCompleted", Av3GateSnapshot.ExternalReviewCompleted, true, "product sign-off"),
            I("L7", "MigrationToAv3 enabled", Av3GateSnapshot.MigrationToAv3Enabled, true, "re-encrypt path"),
            I("L8", "JournalWriter enabled", Av3GateSnapshot.JournalWriterEnabled, true, "authorized"),
            I("L9", "Named product sign-off recorded", true, true, "50.4.0 complete"),
            I("L10", "Installer package released", LabReleaseState.InstallerPackageReleased, false,
                LabReleaseState.SetupFileName),
        };

        return new Report { Items = items };
    }

    private static Item I(string id, string title, bool met, bool req, string note) =>
        new() { Id = id, Title = title, Met = met, RequiredForEnable = req, Note = note };
}
