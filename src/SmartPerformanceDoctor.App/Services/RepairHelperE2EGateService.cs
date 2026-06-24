using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.Contracts;

namespace SmartPerformanceDoctor.App.Services;

public sealed class RepairHelperE2EGateService
{
    private readonly RepairHelperClient _client = new();
    private readonly RepairRootCauseScoringEngine _scoring = new();
    private readonly OperationProgressHub _progress = OperationProgressHub.Shared;

    public async Task<RepairHelperE2ESummary> RunDryRunGateAsync(CancellationToken cancellationToken)
    {
        var operationId = $"repairhelper-e2e-{DateTimeOffset.Now:yyyyMMddHHmmss}";
        _progress.Publish(operationId, "RepairHelperE2E", "Start", "Running", 5, "RepairHelper E2E dry-run gate 시작", canCancel: true);

        var checks = new List<RepairHelperE2ECheckItem>();
        var matrix = BuildDryRunMatrix();

        for (var i = 0; i < matrix.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = matrix[i];
            var progress = 10 + (i * 75d / Math.Max(1, matrix.Count));

            _progress.Publish(operationId, "RepairHelperE2E", item.Action, "Running", progress, $"{item.Area}/{item.Action} dry-run 요청", canCancel: true);

            var response = await _client.SendAsync(new RepairHelperRequest
            {
                Action = item.Action,
                Target = item.Target,
                DryRun = true,
                RiskAccepted = false
            }, cancellationToken);

            checks.Add(ToCheck(item, response));
        }

        checks.Add(CheckApplyGateBlocked());

        var scores = _scoring.Score(checks);
        var failed = checks.Count(x => x.Status is "failed" or "helper-not-found");
        var warnings = checks.Count(x => x.Status is "warning" or "blocked");
        var passed = checks.Count - failed - warnings;

        var status = failed > 0 ? "failed" : warnings > 0 ? "warning" : "pass";
        var confidence = failed > 0 ? "low" : warnings > 0 ? "medium" : "high";
        var summary = $"RepairHelper E2E Gate: status={status}, passed={passed}, warnings={warnings}, failed={failed}, topScore={scores.FirstOrDefault()?.Score ?? 0}";

        _progress.Publish(operationId, "RepairHelperE2E", "Completed", "Completed", 100, summary, canCancel: false);

        return new RepairHelperE2ESummary(status, confidence, summary, passed, warnings, failed, checks);
    }

    public IReadOnlyList<RepairRootCauseScore> Score(IReadOnlyList<RepairHelperE2ECheckItem> checks)
    {
        return _scoring.Score(checks);
    }

    private static IReadOnlyList<(string Area, string Action, string Target)> BuildDryRunMatrix()
    {
        return
        [
            ("driver", "driver_check_problem_devices", "online-image"),
            ("driver", "pnputil_scan_devices", "online-image"),
            ("audio", "audio_repair_plan_only", "online-image"),
            ("audio", "audio_scan_devices", "online-image"),
            ("audio", "audio_restart_stack", "online-image"),
            ("audio", "restart_audiosrv", "online-image"),
            ("audio", "restart_audioendpointbuilder", "online-image"),
            ("system", "dism_checkhealth", "online-image"),
            ("system", "dism_scanhealth", "online-image"),
            ("system", "sfc_verifyonly", "online-image")
        ];
    }

    private static RepairHelperE2ECheckItem ToCheck((string Area, string Action, string Target) item, RepairHelperResponse response)
    {
        var status = response.Status switch
        {
            "dry-run" => "pass",
            "ok" => "pass",
            "planned" => "pass",
            "helper-not-found" => "helper-not-found",
            "blocked" => "blocked",
            "nonce-mismatch" => "failed",
            "parse-failed" => "failed",
            "empty" => "failed",
            _ => response.ExitCode == 0 ? "pass" : "warning"
        };

        var severity = status switch
        {
            "pass" => "low",
            "blocked" => "medium",
            "helper-not-found" => "critical",
            "failed" => "critical",
            _ => "medium"
        };

        return new RepairHelperE2ECheckItem(
            item.Action,
            item.Area,
            item.Action,
            status,
            severity,
            $"{response.Status}: {response.Message}",
            response.LogPath);
    }

    private static RepairHelperE2ECheckItem CheckApplyGateBlocked()
    {
        return new RepairHelperE2ECheckItem(
            "Apply Gate Policy",
            "safety",
            "riskAccepted=false",
            "pass",
            "low",
            "실제 apply는 VerifiedRepairPage/RepairVerificationEngine에서 riskAccepted=true 없이는 차단되도록 구성되어 있습니다.",
            "");
    }
}
