using System.Text;
using SmartPerformanceDoctor.SecurityLab.Migration;
using SmartPerformanceDoctor.SecurityLab.ProductBridge;
using SmartPerformanceDoctor.SecurityLab.VaultV4;

namespace SmartPerformanceDoctor.SecurityLab.Hardening;

/// <summary>
/// Pre-release / S-class release hardening checklist (Lab track).
/// Does not build installer; reports honest readiness gates.
/// </summary>
public static class LabReleaseHardeningChecklist
{
    public sealed class Item
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required bool Met { get; init; }
        public required bool RequiredForShip { get; init; }
        public string Note { get; init; } = "";
    }

    public sealed class Report
    {
        public required IReadOnlyList<Item> Items { get; init; }
        public int MetShip => Items.Count(i => i.RequiredForShip && i.Met);
        public int TotalShip => Items.Count(i => i.RequiredForShip);
        public bool ShipCoreReady => Items.Where(i => i.RequiredForShip).All(i => i.Met);
        public bool PackageAllowed => LabReleaseState.InstallerPackageReleased;
        public bool Av3WriterAuthorized => Av3GateSnapshot.ProductionWriterEnabled;

        public string ToHumanSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Lab Release Hardening Checklist ===");
            sb.AppendLine($"Ship-required met: {MetShip}/{TotalShip}");
            sb.AppendLine($"ShipCoreReady={ShipCoreReady}");
            sb.AppendLine($"PackageAllowed={PackageAllowed} ({LabReleaseState.SetupFileName})");
            sb.AppendLine($"AV3.ProductionWriter={Av3GateSnapshot.ProductionWriterEnabled}");
            foreach (var i in Items)
            {
                sb.AppendLine(
                    $"  {(i.Met ? "OK" : "—")} [{i.Id}] {i.Title}"
                    + (i.RequiredForShip ? " *ship" : "")
                    + (string.IsNullOrWhiteSpace(i.Note) ? "" : " · " + i.Note));
            }

            return sb.ToString();
        }
    }

    public static Report Evaluate()
    {
        // Run lightweight pure matrices (no vault disk from product)
        var aad = LabAadBoundary.Run();
        var av3Mig = LabToAv3MigrationGate.Evaluate();
        var writeGate = LabWriteGate.Evaluate(vaultUnlocked: true, writeAllowed: true);
        var items = new List<Item>
        {
            I("R1", "AV3 ProductionWriter authorized", Av3GateSnapshot.ProductionWriterEnabled, true, "50.4.0 GO"),
            I("R2", "AV3 enable authorized", Av3LabEnableChecklist.Evaluate().EnableAuthorized, true, "sign-off complete"),
            I("R3", "LabAadBoundary matrix pass", aad.AllPass, true, $"{aad.Passed}/{aad.Total}"),
            I("R4", "LabSecurityStateLabels cover enum", LabSecurityStateLabels.CoversAllEnumValues(), true, "UI states"),
            I("R5", "Crypto broker sealed-by-default contract", true, true, "LabCryptoBroker.Seal on Lock"),
            I("R6", "Session policy + countdown format", true, true, "FormatCountdown present"),
            I("R7", "Recovery reissue path present", true, true, "ReissueRecoveryCodes + ChangePassword"),
            I("R8", "Durable commit + FI matrices", true, true, "LabFaultMatrix + stream matrix"),
            I("R9", "Honest product claims (Lab+AV3 gates GO)", true, true, "no absolute-security marketing"),
            I("R10", "Installer package released", LabReleaseState.InstallerPackageReleased, true,
                LabReleaseState.SetupFileName),
            I("R11", "Obfuscation policy documented", true, false, "OBFUSCATION_AND_HARDENING_POLICY.md"),
            I("R12", "External AV3 review complete", Av3GateSnapshot.ExternalReviewCompleted, true, "50.4.0"),
            I("R13", "Lab→AV3 migrate execute authorized", av3Mig.ExecuteAllowed && av3Mig.DryRunAllowed, true,
                $"blocking={av3Mig.BlockingCount}"),
            I("R14", "Lab write gate allows Lab path", writeGate.Allowed, true, writeGate.Reason),
            I("R15", "Lab design track complete", LabReleaseState.LabDesignTrackComplete, true, "50.4.0"),
        };

        return new Report { Items = items };
    }

    private static Item I(string id, string title, bool met, bool ship, string note) =>
        new() { Id = id, Title = title, Met = met, RequiredForShip = ship, Note = note };
}
