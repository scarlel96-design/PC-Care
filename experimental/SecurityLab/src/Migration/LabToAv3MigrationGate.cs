using System.Text;
using SmartPerformanceDoctor.SecurityLab.ProductBridge;

namespace SmartPerformanceDoctor.SecurityLab.Migration;

/// <summary>
/// Phase H: Lab v5 ↔ AV3 migration posture (50.4.0 GO).
/// Execute authorized when product gates are on; migration is re-encrypt only (format non-byte-compatible).
/// </summary>
public static class LabToAv3MigrationGate
{
    public sealed class Gap
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public required bool Blocking { get; init; }
        public string Note { get; init; } = "";
    }

    public sealed class Report
    {
        public required IReadOnlyList<Gap> Gaps { get; init; }
        public bool ExecuteAllowed { get; init; }
        public bool DryRunAllowed { get; init; }
        public string Summary { get; init; } = "";
        public int BlockingCount => Gaps.Count(g => g.Blocking);
        public int TotalGaps => Gaps.Count;

        public string ToHumanSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Lab → AV3 Migration Gate (Phase H) ===");
            sb.AppendLine($"ExecuteAllowed={ExecuteAllowed}");
            sb.AppendLine($"DryRunAllowed={DryRunAllowed}");
            sb.AppendLine($"Blocking gaps: {BlockingCount}/{TotalGaps}");
            sb.AppendLine(Summary);
            foreach (var g in Gaps)
            {
                sb.AppendLine(
                    $"  {(g.Blocking ? "BLOCK" : "OK")} [{g.Id}] {g.Title}"
                    + (string.IsNullOrWhiteSpace(g.Note) ? "" : " · " + g.Note));
            }

            return sb.ToString();
        }
    }

    public static Report Evaluate()
    {
        var writerOn = Av3GateSnapshot.ProductionWriterEnabled;
        var review = Av3GateSnapshot.ExternalReviewCompleted;
        var ready = Av3GateSnapshot.WriterEnableReady;
        var migrate = Av3GateSnapshot.MigrationToAv3Enabled;

        var gaps = new List<Gap>
        {
            G("H1", "AV3 ProductionWriterEnabled", !writerOn, writerOn ? "true" : "false"),
            G("H2", "AV3 ExternalReviewCompleted", !review, review ? "true" : "false"),
            G("H3", "AV3 WriterEnableReady", !ready, ready ? "true" : "false"),
            G("H4", "MigrationToAv3Enabled flag", !migrate, migrate ? "true" : "false"),
            G("H5", "Named product sign-off (50.4.0)", false, "product complete authorization"),
            G("H6", "Binary header/format parity", false,
                "re-encrypt migration (not in-place) — authorized path"),
            G("H7", "Object container magic mapping", false,
                "SPDCHK4 ↔ AV3 via re-encrypt"),
            G("H8", "Recovery model mapping", false, "re-issue codes on migrate"),
            G("H9", "Journal schema mapping", false, "rebuild journal on migrate"),
            G("H10", "Lab shipping path complete", false, "Lab v5 production vault active"),
        };

        var execute = writerOn && review && ready && migrate && gaps.Count(g => g.Blocking) == 0;
        return new Report
        {
            Gaps = gaps,
            ExecuteAllowed = execute,
            DryRunAllowed = true,
            Summary = execute
                ? "Execute authorized (re-encrypt migration). Flags: " + Av3GateSnapshot.StatusSummary
                : "Execute not authorized. Flags: " + Av3GateSnapshot.StatusSummary
        };
    }

    public static (bool Ok, string Message) TryAuthorizeExecute()
    {
        var r = Evaluate();
        if (!r.ExecuteAllowed)
        {
            return (false, "Lab→AV3 마이그레이션 실행 거부: " + r.Summary);
        }

        return (true,
            "Lab→AV3 마이그레이션 실행 승인 (재암호화 경로). 원본 Lab 금고는 유지·백업 권고.");
    }

    private static Gap G(string id, string title, bool blocking, string note) =>
        new() { Id = id, Title = title, Blocking = blocking, Note = note };
}
