using System.Diagnostics;
using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.ViewModels;
using SmartPerformanceDoctor.Contracts;
using SmartPerformanceDoctor.Contracts.Services;

namespace SmartPerformanceDoctor.App.Services;

public sealed class UnifiedCareService
{
    private readonly RepairHelperClient _repairClient = new();
    private readonly InferenceOrchestrator _inference = new();

    public async Task<CareSessionResult> RunAsync(
        CareRequest request,
        Action<CareStepResult> onStep,
        CancellationToken cancellationToken)
    {
        var session = UnifiedCareAuditService.BeginSession(request);
        UnifiedCareAuditService.Append(session, "session-start", $"범위 {request.Scope}", true);

        var steps = new List<CareStepResult>();
        var sw = Stopwatch.StartNew();
        var moduleIds = ScopeRepairFilter.ResolveModuleIds(request.Scope);
        var diagnoses = new List<IntelligenceSummary>();
        var rawSignals = new List<string>();
        var reportDirs = new List<string>();
        var actionsTaken = new List<string>();
        int? scoreBefore = null;
        int? scoreAfter = null;
        InferenceResult? inference = null;

        foreach (var moduleId in moduleIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var module = ModuleRegistry.Get(moduleId);
            onStep(RunningStep("diagnosis", $"{module.Title} 점검 중", "진단 엔진이 PC 상태를 분석하고 있습니다.", "점검"));

            try
            {
                var vm = new ModuleViewModel();
                vm.ResetForModule(module);
                await vm.RunAsync(module, cancellationToken, evt =>
                {
                    var detail = DiagnosticMessageLocalizer.Localize(evt.Message);
                    onStep(RunningStep(
                        "diagnosis",
                        $"{module.Title} — {evt.Progress}%",
                        detail,
                        "점검"));
                }).ConfigureAwait(false);

                if (vm.Intelligence is not null)
                {
                    diagnoses.Add(vm.Intelligence);
                    scoreBefore ??= vm.Intelligence.Score;
                }

                rawSignals.AddRange(vm.Events.Select(e => e.Message));

                try
                {
                    KnowledgeService.Shared.RecordEngineRun(
                        moduleId,
                        vm.Status,
                        vm.Intelligence?.Score ?? 0,
                        vm.Events.Select(e => e.Message).ToArray(),
                        vm.Intelligence,
                        sw.ElapsedMilliseconds);

                    if (!string.IsNullOrWhiteSpace(vm.JsonReportPath))
                    {
                        var dir = Path.GetDirectoryName(vm.JsonReportPath);
                        if (!string.IsNullOrWhiteSpace(dir))
                        {
                            reportDirs.Add(dir);
                            KnowledgeService.Shared.Database.IndexReport(vm.HtmlReportPath ?? vm.JsonReportPath!, moduleId);
                        }
                    }
                }
                catch
                {
                    // DB write failures must not terminate the session.
                }

                var diagnosisOk = string.Equals(vm.Status, "ok", StringComparison.OrdinalIgnoreCase)
                    || vm.Intelligence is not null;
                var diagnosisStep = new CareStepResult(
                    "diagnosis",
                    $"{module.Title} 점검 완료",
                    FormatDiagnosisDetail(vm),
                    "점검",
                    diagnosisOk);
                steps.Add(diagnosisStep);
                onStep(diagnosisStep);
                UnifiedCareAuditService.Append(session, "diagnosis", module.Title, diagnosisStep.Success);
            }
            catch (Exception ex)
            {
                var failed = new CareStepResult(
                    "diagnosis",
                    $"{module.Title} 점검 오류",
                    $"점검 중 예외가 발생했지만 프로그램은 계속 실행합니다.\n{ex.Message}",
                    "점검",
                    false);
                steps.Add(failed);
                onStep(failed);
            }
        }

        onStep(RunningStep("inference", "지능 분석 진행", "규칙 DB, 학습 기록, 신호 상관 분석을 융합합니다.", "지능 분석"));
        try
        {
            inference = _inference.Analyze(request.Scope, diagnoses, rawSignals);
            var inferenceStep = new CareStepResult(
                "inference",
                "지능 분석 완료",
                FormatInferenceDetail(inference),
                "지능 분석",
                true);
            steps.Add(inferenceStep);
            onStep(inferenceStep);

            if (inference.EnhancedIntelligence is not null)
            {
                scoreBefore ??= inference.FusedScore;
            }
        }
        catch (Exception ex)
        {
            var inferenceFailed = new CareStepResult(
                "inference",
                "지능 분석 제한 모드",
                $"분석 엔진을 완전히 실행하지 못했습니다. 기본 규칙으로 계속합니다.\n{ex.Message}",
                "지능 분석",
                false);
            steps.Add(inferenceFailed);
            onStep(inferenceFailed);
        }

        if (!request.IncludeRepair)
        {
            var onlyDiag = new CareStepResult(
                "summary",
                "진단만 완료",
                "복구 옵션이 꺼져 있어 실제 변경은 수행하지 않았습니다. 점검·추론 결과만 저장되었습니다.",
                "점검만",
                true);
            steps.Add(onlyDiag);
            onStep(onlyDiag);

            FinalizeReportBundles(reportDirs, actionsTaken, includeRepair: false);
            return FinishSession(session, request, steps, scoreBefore, null, true,
                $"점검 완료 · {diagnoses.Count}개 영역 · AI 추론 반영 · 복구는 수행하지 않음");
        }

        if (!request.RiskAccepted)
        {
            var blocked = new CareStepResult(
                "repair",
                "복구 대기",
                "복구를 실행하려면 '위험도를 확인했습니다'에 체크한 뒤 다시 시작하세요.",
                "생략",
                false);
            steps.Add(blocked);
            onStep(blocked);
            FinalizeReportBundles(reportDirs, actionsTaken, includeRepair: false);
            return FinishSession(session, request, steps, scoreBefore, null, false,
                "점검·추론은 완료됐지만 복구 승인이 없어 복구 단계를 건너뛰었습니다.");
        }

        var plan = RepairPlanMapper.BuildPlan(request.Scope, diagnoses, onlyWhenIssuesFound: true, inference);
        if (plan.Count == 0)
        {
            var noRepair = new CareStepResult(
                "summary",
                "복구 불필요",
                "점검·AI 추론 결과 심각한 문제가 없어 자동 복구 작업을 건너뛰었습니다.",
                "생략",
                true);
            steps.Add(noRepair);
            onStep(noRepair);
            actionsTaken.Add("정밀 스캔 결과 문제 없음 — 자동 복구 건너뜀");
            FinalizeReportBundles(reportDirs, actionsTaken, includeRepair: true);
            return FinishSession(session, request, steps, scoreBefore, scoreBefore, true,
                "점검·추론 완료 · 자동 복구할 항목 없음");
        }

        UnifiedCareSnapshotService.CapturePreRepair(session, moduleIds);
        onStep(RunningStep("repair", "복구 계획 확정", $"{plan.Count}개 안전 복구 작업을 순서대로 실행합니다.", "복구 준비"));

        foreach (var action in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var simulate = await _repairClient.SendAsync(new RepairHelperRequest
                {
                    Action = action.Id,
                    Target = action.DefaultTarget,
                    DryRun = true,
                    RiskAccepted = false
                }, cancellationToken);

                var simulateStep = new CareStepResult(
                    "repair",
                    $"{action.Title} — 사전 확인",
                    FormatRepairDetail(action, simulate, simulated: true),
                    "사전 확인",
                    simulate.Status is "dry-run" or "ok" or "planned",
                    simulate.LogPath);
                steps.Add(simulateStep);
                onStep(simulateStep);

                var apply = await _repairClient.SendAsync(new RepairHelperRequest
                {
                    Action = action.Id,
                    Target = action.DefaultTarget,
                    DryRun = false,
                    RiskAccepted = true
                }, cancellationToken);

                var applyStep = new CareStepResult(
                    "repair",
                    $"{action.Title} — 실제 복구",
                    FormatRepairDetail(action, apply, simulated: false),
                    "복구 적용",
                    apply.Status is "ok" or "planned" && (apply.ExitCode ?? 1) == 0,
                    apply.LogPath);
                steps.Add(applyStep);
                onStep(applyStep);
                actionsTaken.Add($"{action.Title} — {(apply.Status is "ok" or "planned" && (apply.ExitCode ?? 1) == 0 ? "완료" : "실패")} · {apply.Message}");

                try
                {
                    KnowledgeService.Shared.RecordRepairOutcome(
                        request.Scope,
                        action.Id,
                        dryRun: false,
                        apply.Status,
                        apply.ExitCode ?? -1,
                        apply.Message);
                }
                catch
                {
                    // ignored
                }
            }
            catch (Exception ex)
            {
                var repairFailed = new CareStepResult(
                    "repair",
                    $"{action.Title} — 복구 오류",
                    $"복구 단계에서 오류가 발생했습니다. 다음 작업으로 계속합니다.\n{ex.Message}",
                    "복구 적용",
                    false);
                steps.Add(repairFailed);
                onStep(repairFailed);
            }
        }

        onStep(RunningStep("verify", "복구 결과 확인", "점검 범위 전체를 다시 점검해 복구 효과를 비교합니다.", "재점검"));

        var verifyScores = new List<int>();
        try
        {
            foreach (var moduleId in moduleIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var verifyVm = new ModuleViewModel();
                var verifyModule = ModuleRegistry.Get(moduleId);
                verifyVm.ResetForModule(verifyModule);
                await verifyVm.RunAsync(verifyModule, cancellationToken);
                if (verifyVm.Intelligence?.Score is int s)
                {
                    verifyScores.Add(s);
                }
            }

            scoreAfter = verifyScores.Count > 0
                ? (int)verifyScores.Average()
                : inference?.FusedScore;

            UnifiedCareSnapshotService.CapturePostRepair(session, scoreBefore, scoreAfter);
            var verifyStep = new CareStepResult(
                "verify",
                "복구 후 전 모듈 재점검",
                scoreBefore.HasValue && scoreAfter.HasValue
                    ? $"점수 변화: {scoreBefore}점 → {scoreAfter}점 (Δ {scoreAfter - scoreBefore})\n재점검 모듈 {moduleIds.Count}개"
                    : $"재점검 모듈 {moduleIds.Count}개 완료",
                "재점검",
                true);
            steps.Add(verifyStep);
            onStep(verifyStep);
            UnifiedCareAuditService.Append(session, "verify", "전 모듈 재점검", true);
        }
        catch (Exception ex)
        {
            var verifyFailed = new CareStepResult(
                "verify",
                "복구 후 재점검 제한",
                $"재점검을 완료하지 못했습니다.\n{ex.Message}",
                "재점검",
                false);
            steps.Add(verifyFailed);
            onStep(verifyFailed);
            UnifiedCareAuditService.Append(session, "verify", "재점검 실패", false);
        }

        var improved = scoreBefore.HasValue && scoreAfter.HasValue && scoreAfter >= scoreBefore;
        var summary = improved
            ? $"점검·복구 완료 · 점수 {scoreBefore}→{scoreAfter} · {plan.Count}개 작업 실행"
            : $"점검·복구 완료 · {plan.Count}개 작업 실행 · 재점검 결과를 확인하세요";

        FinalizeReportBundles(reportDirs, actionsTaken, request.IncludeRepair);
        return FinishSession(session, request, steps, scoreBefore, scoreAfter, true, summary);
    }

    private static CareSessionResult FinishSession(
        UnifiedCareSessionContext session,
        CareRequest request,
        List<CareStepResult> steps,
        int? scoreBefore,
        int? scoreAfter,
        bool completed,
        string summary)
    {
        var delta = scoreBefore.HasValue && scoreAfter.HasValue ? scoreAfter - scoreBefore : (int?)null;
        var auditValid = UnifiedCareAuditService.Verify(session.AuditFolder);
        UnifiedCareAuditService.Append(session, "session-complete", summary, completed);
        var result = BuildResult(request, steps, scoreBefore, scoreAfter, completed, summary, session, delta, auditValid);
        UnifiedCareAuditService.Complete(session, result);
        return result;
    }

    private static void FinalizeReportBundles(
        IReadOnlyList<string> reportDirs,
        IReadOnlyList<string> actionsTaken,
        bool includeRepair)
    {
        var taken = actionsTaken.Count > 0
            ? actionsTaken
            : new[] { includeRepair ? "복구 승인 없음 또는 복구 항목 없음" : "진단 스캔만 수행 · PC 설정 변경 없음" };

        foreach (var dir in reportDirs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            ReportBundleEnricher.AppendActionsTaken(dir, taken, includeRepair);
        }
    }

    private static CareSessionResult BuildResult(
        CareRequest request,
        List<CareStepResult> steps,
        int? scoreBefore,
        int? scoreAfter,
        bool completed,
        string summary,
        UnifiedCareSessionContext? session = null,
        int? healthDelta = null,
        bool auditChainValid = true)
    {
        return new CareSessionResult(
            request.Scope,
            request.IncludeRepair,
            completed,
            summary,
            scoreBefore,
            scoreAfter,
            steps,
            session?.SessionId,
            session?.AuditFolder,
            healthDelta,
            auditChainValid);
    }

    private static CareStepResult RunningStep(string phase, string title, string detail, string kind) =>
        new(phase, title, detail, kind, true);

    private static string FormatDiagnosisDetail(ModuleViewModel vm)
    {
        if (vm.Intelligence is null)
        {
            return vm.LatestMessage;
        }

        var lines = new List<string>
        {
            $"상태: {vm.Intelligence.Status}",
            $"점수: {vm.Intelligence.Score}점",
            vm.Intelligence.PlainSummary
        };

        if (vm.Intelligence.RootCauses.Count > 0)
        {
            lines.Add("");
            lines.Add("발견된 문제:");
            foreach (var cause in vm.Intelligence.RootCauses.Take(3))
            {
                lines.Add($"• {cause.Explanation}");
            }
        }

        if (!string.IsNullOrWhiteSpace(vm.HtmlReportPath))
        {
            lines.Add($"보고서: {vm.HtmlReportPath}");
        }

        return string.Join('\n', lines);
    }

    private static string FormatInferenceDetail(InferenceResult inference)
    {
        var lines = new List<string>
        {
            $"융합 점수: {inference.FusedScore}점",
            $"상태: {inference.Status}",
            inference.Summary
        };

        if (inference.Insights.Count > 0)
        {
            lines.Add("");
            lines.Add("AI·규칙 인사이트:");
            foreach (var insight in inference.Insights.Take(4))
            {
                lines.Add($"• [{insight.Source}] {insight.Title} (신뢰도 {insight.Confidence:P0})");
            }
        }

        if (inference.RecommendedRepairActionIds.Count > 0)
        {
            lines.Add("");
            lines.Add("추천 복구 작업:");
            foreach (var actionId in inference.RecommendedRepairActionIds.Take(5))
            {
                lines.Add($"• {actionId}");
            }
        }

        return string.Join('\n', lines);
    }

    private static string FormatRepairDetail(RepairActionDescriptor action, RepairHelperResponse response, bool simulated)
    {
        var mode = simulated ? "사전 확인(변경 없음)" : "복구 적용";
        return
            $"모드: {mode}\n" +
            $"작업: {action.Title}\n" +
            $"결과: {response.Status}\n" +
            $"메시지: {response.Message}\n" +
            $"종료 코드: {response.ExitCode}\n" +
            $"관리자 권한: {(response.Elevated ? "사용" : "미사용")}\n" +
            (string.IsNullOrWhiteSpace(response.LogPath) ? "" : $"기록: {response.LogPath}");
    }
}