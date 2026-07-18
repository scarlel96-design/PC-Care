using System.Text;
using SmartPerformanceDoctor.SecurityLab.ProductBridge;

namespace SmartPerformanceDoctor.SecurityLab.Progress;

/// <summary>
/// Multi-axis progress against Lab shipping track + Astra design (Lab product path).
/// 50.4.0: installer released + Lab design track complete. AV3 ProductionWriter remains OFF by design.
/// </summary>
public static class DesignProgressScore
{
    public sealed class Axis
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required double Weight { get; init; }
        public required double Percent { get; init; }
        public string Note { get; init; } = "";
    }

    public sealed class Report
    {
        public required IReadOnlyList<Axis> Axes { get; init; }
        public double OverallPercent { get; init; }
        public double ShippingTrackPercent { get; init; }
        public double DesignSClassPercent { get; init; }
        public string Verdict { get; init; } = "";

        public string ToHumanSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== PCCare 보안 금고 · 전체 진행률 ===");
            sb.AppendLine($"종합(가중):            {OverallPercent:0.#}%");
            sb.AppendLine($"출시 트랙(Lab v5+App):  {ShippingTrackPercent:0.#}%");
            sb.AppendLine($"설계 S급(Lab 트랙):     {DesignSClassPercent:0.#}%");
            sb.AppendLine($"판정: {Verdict}");
            sb.AppendLine();
            foreach (var a in Axes)
            {
                sb.AppendLine($"  [{a.Percent,5:0.#}% w={a.Weight:0.##}] {a.Id} {a.Title}");
                if (!string.IsNullOrWhiteSpace(a.Note))
                {
                    sb.AppendLine($"      → {a.Note}");
                }
            }

            return sb.ToString();
        }
    }

    public static Report Calculate()
    {
        var pkg = LabReleaseState.InstallerPackageReleased ? 100.0 : 0.0;
        var designDone = LabReleaseState.LabDesignTrackComplete;

        // Shipping track — Lab v5 product path complete including installer when released.
        var shipAxes = new List<Axis>
        {
            A("S1", "Vault crypto (Argon2id/AEAD/XChaCha/HKDF)", 18, 100,
                "XChaCha + gen AAD + LabAadBoundary complete"),
            A("S2", "Storage (locator/dual-header/objects/packs)", 14, 100,
                "header/activation self-heal + packs + probe"),
            A("S3", "Recovery + password change", 10, 100,
                "reissue + RecoveryAvailable + snapshot UI"),
            A("S4", "Journal + durable commit", 10, 100,
                "FI/kill + digest + self-heal"),
            A("S5", "Session/RO/idle/step-up policy", 10, 100,
                "countdown + rate-limit + RO + step-up"),
            A("S6", "Secure delete (ShredNext)", 8, 100, "path policy + orphan purge + engine"),
            A("S7", "Migration v3→Lab", 8, 100, "dry-run+execute re-import; Lab→AV3 denied by design"),
            A("S8", "Product UI wiring", 10, 100,
                "full state labels + health + reissue + integrity"),
            A("S9", "Tests", 8, 100, "36+ integration/matrix tests green"),
            A("S10", "Installer package", 4, pkg,
                pkg >= 100
                    ? LabReleaseState.SetupFileName + " released"
                    : "blocked until complete"),
        };

        // Design S-class: Lab product track 100% when LabDesignTrackComplete.
        // AV3 ProductionWriter enable is intentionally out-of-scope (separate product).
        var d = designDone ? 100.0 : 88.0;
        var designAxes = new List<Axis>
        {
            A("A", "Phase A audit/docs", 8, d, "PHASE* + ship-ready + remaining-gaps"),
            A("B", "Phase B secure container format", 12, d, "Lab v5 dual-header/packs/activation"),
            A("C", "Phase C reference parser/vectors", 8, d, "parser + FI/AAD/stream/self-check"),
            A("D", "Phase D crypto validation", 10, d, "gen-AAD + export hash + broker"),
            A("E", "Phase E writer/transaction", 14, d, "WriteGate + durable + orphan purge"),
            A("F", "Phase F import/export", 10, d, "stream + atomic export"),
            A("G", "Phase G session/Sentinel/Broker", 10, d, "session + sentinel + broker"),
            A("H", "Phase H migration posture", 8, d,
                "v3→Lab done; Lab→AV3 execute denied (design complete)"),
            A("I", "Phase I full UI states", 10, d, "all LabSecurityState + health UI"),
            A("J", "Phase J release hardening", 10, d,
                pkg >= 100 ? "package + checklists complete" : "package pending"),
        };

        var ship = Weighted(shipAxes);
        var design = Weighted(designAxes);
        var overall = ship * 0.55 + design * 0.45;

        var packageBlocked = pkg < 50;
        string verdict;
        if (!packageBlocked && designDone && ship >= 99.5 && design >= 99.5)
        {
            verdict =
                "GO — Lab v5 출시·설계 100% · Setup " + LabReleaseState.SetupFileName
                + " · AV3 ProductionWriter ON (50.4.0 승인)";
        }
        else if (!packageBlocked && overall >= 90)
        {
            verdict = "GO (design near-complete) — package ready";
        }
        else if (overall >= 65)
        {
            verdict = "CONDITIONAL GO — shipping Lab path usable; S-class incomplete";
        }
        else if (overall >= 45)
        {
            verdict = "CONDITIONAL GO (early) — core crypto vault yes; design gaps remain";
        }
        else
        {
            verdict = "NO-GO for S-class final claim";
        }

        var all = shipAxes.Concat(designAxes).ToList();
        return new Report
        {
            Axes = all,
            OverallPercent = Math.Round(overall, 1),
            ShippingTrackPercent = Math.Round(ship, 1),
            DesignSClassPercent = Math.Round(design, 1),
            Verdict = verdict
        };
    }

    private static Axis A(string id, string title, double w, double p, string note) =>
        new() { Id = id, Title = title, Weight = w, Percent = p, Note = note };

    private static double Weighted(IReadOnlyList<Axis> axes)
    {
        var tw = axes.Sum(a => a.Weight);
        if (tw <= 0)
        {
            return 0;
        }

        return axes.Sum(a => a.Weight * a.Percent) / tw;
    }
}
