using System.Text;
using SmartPerformanceDoctor.SecurityLab.Hardening;

namespace SmartPerformanceDoctor.SecurityLab.VaultV4;

/// <summary>
/// Locked-safe vault health snapshot for UI/CLI (no content decrypt).
/// </summary>
public static class LabVaultHealth
{
    public sealed class Report
    {
        public required string Root { get; init; }
        public required bool Exists { get; init; }
        public required LabContainerProbe.Report Container { get; init; }
        public required LabRecoverySlots.RecoverySnapshot Recovery { get; init; }
        public required LabRateLimiter.Snapshot RateLimit { get; init; }
        public bool ActivationPresent { get; init; }
        public bool OverallOk { get; init; }

        public string ToHumanSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Lab Vault Health ===");
            sb.AppendLine($"Root: {Root}");
            sb.AppendLine($"Exists={Exists} OverallOk={OverallOk}");
            sb.AppendLine($"Activation={(ActivationPresent ? "present" : "missing")}");
            sb.AppendLine(Recovery.ToUiLine());
            sb.AppendLine(RateLimit.ToUiLine());
            sb.AppendLine(Container.ToHumanSummary().TrimEnd());
            return sb.ToString();
        }

        public string ToUiLine()
        {
            if (!Exists)
            {
                return "금고 없음";
            }

            var parts = new List<string>
            {
                OverallOk ? "건강 OK" : "건강 경고",
                $"컨테이너 {Container.Passed}/{Container.Total}",
                Recovery.ToUiLine(),
                RateLimit.IsLocked ? RateLimit.ToUiLine() : (RateLimit.Failures > 0 ? RateLimit.ToUiLine() : "제한 없음")
            };
            return string.Join(" · ", parts);
        }
    }

    public static Report Probe(string vaultRoot)
    {
        var root = Path.GetFullPath(vaultRoot);
        var exists = LabVaultService.Exists(root);
        var container = LabContainerProbe.Probe(root);
        var rec = LabRecoverySlots.Snapshot(root);
        var rate = LabRateLimiter.GetSnapshot(root);
        var act = LabDurableCommit.TryRead(root) is not null
                  || File.Exists(Path.Combine(root, LabDurableCommit.FileName));
        var ok = exists
                 && container.Healthy
                 && rec.Format != "corrupt"
                 && !rate.IsLocked;

        return new Report
        {
            Root = root,
            Exists = exists,
            Container = container,
            Recovery = rec,
            RateLimit = rate,
            ActivationPresent = act,
            OverallOk = ok
        };
    }
}
