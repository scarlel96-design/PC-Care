using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.Contracts;

namespace SmartPerformanceDoctor.App.Services;

public sealed class RepairVerificationEngine
{
    private readonly RepairHelperClient _client = new();
    private readonly OperationProgressHub _progress = OperationProgressHub.Shared;
    private readonly RepairExecutionAuditStore _auditStore = new();

    public async Task<RepairVerificationResult> VerifyAsync(
        IntelligentRepairPlan plan,
        string target,
        bool apply,
        bool riskAccepted,
        CancellationToken cancellationToken)
    {
        var operationId = $"verified-{plan.Area}-{DateTimeOffset.Now:yyyyMMddHHmmss}";
        _progress.Publish(operationId, "RepairIntelligence", "PreCheck", "Running", 5, $"{plan.Area} pre-check 시작", canCancel: true);

        var before = BuildLocalSnapshot(plan.Area);

        _progress.Publish(operationId, "RepairIntelligence", "DryRun", "Running", 20, $"{plan.PrimaryAction} dry-run 준비", canCancel: true);
        var dryRun = await _client.SendAsync(new RepairHelperRequest
        {
            Action = plan.PrimaryAction,
            Target = string.IsNullOrWhiteSpace(target) ? "online-image" : target.Trim(),
            DryRun = true,
            RiskAccepted = false
        }, cancellationToken);

        RepairHelperResponse? applyResponse = null;
        if (apply)
        {
            _progress.Publish(operationId, "RepairIntelligence", "Apply", "Running", 55, $"{plan.PrimaryAction} apply 실행", canCancel: true);
            applyResponse = await _client.SendAsync(new RepairHelperRequest
            {
                Action = plan.PrimaryAction,
                Target = string.IsNullOrWhiteSpace(target) ? "online-image" : target.Trim(),
                DryRun = false,
                RiskAccepted = riskAccepted
            }, cancellationToken);
        }

        _progress.Publish(operationId, "RepairIntelligence", "PostCheck", "Running", 78, $"{plan.Area} post-check 시작", canCancel: true);
        var after = BuildLocalSnapshot(plan.Area);

        var evidence = BuildEvidence(plan, before, after, dryRun, applyResponse, apply);
        var confidence = ScoreConfidence(evidence, apply, applyResponse);
        var status = evidence.Any(x => x.Verdict == "failed")
            ? "failed"
            : evidence.Any(x => x.Verdict == "partial")
                ? "partial"
                : apply ? "verified" : "planned";

        var result = new RepairVerificationResult(
            operationId,
            plan.Area,
            plan.PrimaryAction,
            status,
            confidence,
            BuildSummary(plan, status, confidence, apply),
            before,
            after,
            applyResponse?.LogPath ?? dryRun.LogPath,
            evidence,
            BuildNextActions(plan, status, confidence));

        var auditPath = _auditStore.Save(result);
        _progress.Publish(operationId, "RepairIntelligence", "Audit", "Completed", 100, $"검증형 복구 감사 로그 저장: {auditPath}", canCancel: false);

        return result with { LogPath = string.IsNullOrWhiteSpace(result.LogPath) ? auditPath : result.LogPath };
    }

    private static string BuildLocalSnapshot(string area)
    {
        var baseDir = AppContext.BaseDirectory;
        var desktopRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SmartPerformanceDoctor");
        var snapshot = new
        {
            area,
            timestamp = DateTimeOffset.Now,
            baseDir,
            userRoot = desktopRoot,
            reports = Directory.Exists(Path.Combine(desktopRoot, "Reports")) ? Directory.GetFiles(Path.Combine(desktopRoot, "Reports")).Length : 0,
            repairLogs = Directory.Exists(Path.Combine(desktopRoot, "RepairLogs")) ? Directory.GetFiles(Path.Combine(desktopRoot, "RepairLogs")).Length : 0,
            crashLogs = Directory.Exists(Path.Combine(desktopRoot, "CrashLogs")) ? Directory.GetFiles(Path.Combine(desktopRoot, "CrashLogs")).Length : 0,
            processMemoryMb = Environment.WorkingSet / 1024 / 1024,
            os = Environment.OSVersion.ToString(),
            runtime = Environment.Version.ToString()
        };

        return System.Text.Json.JsonSerializer.Serialize(snapshot);
    }

    private static IReadOnlyList<RepairEvidence> BuildEvidence(
        IntelligentRepairPlan plan,
        string before,
        string after,
        RepairHelperResponse dryRun,
        RepairHelperResponse? applyResponse,
        bool apply)
    {
        var list = new List<RepairEvidence>
        {
            new(
                "dry-run",
                "RepairHelper",
                "requested",
                dryRun.Status,
                string.Equals(dryRun.Status, "ok", StringComparison.OrdinalIgnoreCase) || string.Equals(dryRun.Status, "planned", StringComparison.OrdinalIgnoreCase) ? "passed" : "partial",
                dryRun.Message),

            new(
                "pre-post-snapshot",
                "App",
                before.Length.ToString(),
                after.Length.ToString(),
                before == after ? "partial" : "passed",
                "복구 전후 앱 관측 스냅샷을 비교했습니다.")
        };

        if (!apply)
        {
            list.Add(new RepairEvidence(
                "apply",
                "Policy",
                "not-requested",
                "skipped",
                "partial",
                "Dry-run만 수행했습니다. 실제 변경 검증은 apply 후 가능합니다."));
            return list;
        }

        if (applyResponse is null)
        {
            list.Add(new RepairEvidence(
                "apply",
                "RepairHelper",
                "requested",
                "missing-response",
                "failed",
                "apply 응답이 없습니다."));
            return list;
        }

        list.Add(new RepairEvidence(
            "apply",
            "RepairHelper",
            "requested",
            applyResponse.Status,
            applyResponse.ExitCode == 0 ? "passed" : "failed",
            $"{applyResponse.Message} / exit={applyResponse.ExitCode}"));

        list.Add(new RepairEvidence(
            "log",
            "RepairHelper",
            "",
            applyResponse.LogPath,
            string.IsNullOrWhiteSpace(applyResponse.LogPath) ? "partial" : "passed",
            "RepairHelper 실행 로그 경로입니다."));

        return list;
    }

    private static string ScoreConfidence(IReadOnlyList<RepairEvidence> evidence, bool apply, RepairHelperResponse? applyResponse)
    {
        var passed = evidence.Count(x => x.Verdict == "passed");
        var failed = evidence.Count(x => x.Verdict == "failed");

        if (!apply)
        {
            return "plan-only";
        }

        if (failed > 0)
        {
            return "low";
        }

        if (applyResponse?.ExitCode == 0 && passed >= 3)
        {
            return "high";
        }

        return passed >= 2 ? "medium" : "low";
    }

    private static string BuildSummary(IntelligentRepairPlan plan, string status, string confidence, bool apply)
    {
        return $"{plan.Area}/{plan.PrimaryAction}: status={status}, confidence={confidence}, mode={(apply ? "apply+verify" : "dry-run+plan")}";
    }

    private static IReadOnlyList<string> BuildNextActions(IntelligentRepairPlan plan, string status, string confidence)
    {
        if (status == "verified" && confidence == "high")
        {
            return ["보고서와 복구 로그를 보관하세요.", "동일 증상이 재발하면 같은 PlanId로 비교하세요."];
        }

        if (plan.Area == "audio")
        {
            return ["오디오 장치 재스캔을 실행하세요.", "Bluetooth 오디오라면 장치 제거 없이 재페어링/서비스 재시작을 우선하세요.", "복구 로그와 이벤트 로그를 오류 번들로 묶으세요."];
        }

        if (plan.Area == "driver")
        {
            return ["문제 장치 InstanceId를 확인하세요.", "pnputil_scan_devices 후 문제 장치 수 변화를 확인하세요.", "장치 재시작은 정확한 InstanceId로만 수행하세요."];
        }

        return ["DISM CheckHealth → ScanHealth → RestoreHealth 순서로 진행하세요.", "SFC verifyonly 후 scannow를 판단하세요.", "DISM 63% 정체 시 heartbeat log를 확인하세요."];
    }
}
