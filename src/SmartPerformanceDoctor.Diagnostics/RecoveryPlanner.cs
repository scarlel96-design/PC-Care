using System.Security.Cryptography;
using System.Text;

namespace SmartPerformanceDoctor.Diagnostics;

public enum RecoveryAction { ReviewStartup, ReviewBackground, ReviewUserCache, ReviewDiskSpace, CheckWindowsComponents, CheckUpdateState, ReviewApplicationLoad, ReviewServiceConfiguration }
public sealed record RecoveryPlan(string PlanId, string FindingId, IReadOnlyList<string> EvidenceReferences,
    string ProblemDescription, RecoveryAction ProposedAction, string Eligibility, IReadOnlyList<string> ConfidenceLimitations,
    string RiskLevel, string RequiredPrivileges, string ExpectedEffect, string EstimatedResourceCost,
    bool UserConsentRequired, IReadOnlyList<string> PreflightChecks, string RollbackCapability,
    string RollbackPlan, IReadOnlyList<string> VerificationMetrics, string ExecutionState);
public sealed record RecoveryDryRun(string PlanId, string Status, string Target, IReadOnlyList<string> Preconditions,
    string RequiredPrivileges, string Risk, string RollbackCapability, IReadOnlyList<string> VerificationMetrics,
    bool ExecutionEnabled, int SystemMutations);

/// <summary>Build review plans. This assembly has no recovery executor.</summary>
public static class RecoveryPlanner
{
    public static IReadOnlyList<RecoveryPlan> Create(IReadOnlyList<Finding> findings) => findings.Take(256)
        .Where(f => !f.Category.StartsWith("NoObservedPressure.", StringComparison.Ordinal)
            && !f.Category.StartsWith("Transient.", StringComparison.Ordinal))
        .Select(f => Create(f, SuggestedAction(f.Category))).ToArray();

    public static RecoveryPlan Create(Finding finding, RecoveryAction action)
    {
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
        var supported = action == SuggestedAction(finding.Category) && IsSupportedCategory(finding.Category);
        var measured = finding.SampleCount >= 8 && double.IsFinite(finding.EvidenceCoverage)
            && finding.EvidenceCoverage is >= .8 and <= 1 && double.IsFinite(finding.MeasurementQuality)
            && finding.MeasurementQuality is >= .5 and <= 1;
        var eligible = supported && measured && finding.Severity is "Candidate" or "ConfirmedObservation";
        var reason = finding.Severity == "ExpectedWorkload" ? "ExpectedWorkload" : !supported ? "UnsupportedEvidence"
            : !measured ? "InsufficientEvidence" : "ObservationRequiresConfirmation";
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(finding.FindingId + ":" + action + ":" + finding.MeasurementWindow)))[..24];
        return new(id, finding.FindingId, [finding.FindingId, finding.MeasurementWindow, finding.SourceProvider],
            DiagnosticPresentation.Category(finding.Category), action, eligible ? "ReviewOnly" : "Blocked:" + reason,
            finding.Limitations.Concat(["근본 원인과 실제 개선 효과는 미확인입니다."]).ToArray(),
            "ReviewOnly;ExecutionRiskNotAssessed", "NoneForPreview;ReassessBeforeExecution", "Unknown",
            "UnknownUntilTargetInspection", true,
            ["동일한 작업 조건에서 재측정하세요.", "정확한 변경 대상과 소유자를 확인하세요.", "현재 상태와 원복 자료를 보존하세요.", "실행 전 별도 동의를 받으세요."],
            "NotApplicableToPreview;RequiredBeforeExecution", "미리보기는 상태를 변경하지 않습니다. 실행 단계에서 조치별 원복을 설계하세요.",
            ["동일 시간 구간의 CPU 중앙값·P95", "메모리 압박 시간", "장치별 I/O 지연", "사용자 작업 완료 시간"], "PlannedOnly");
    }

    public static RecoveryDryRun DryRun(RecoveryPlan plan)
    {
        var valid = Enum.IsDefined(plan.ProposedAction) && plan.ExecutionState == "PlannedOnly"
            && plan.UserConsentRequired && plan.ExpectedEffect == "Unknown" && plan.EvidenceReferences.Count >= 3
            && plan.PreflightChecks.Count >= 4 && plan.VerificationMetrics.Count > 0;
        var status = !valid ? "Blocked:InvalidPlan" : plan.Eligibility == "ReviewOnly" ? "PreviewAvailable" : plan.Eligibility;
        return new(plan.PlanId, status, "ExactTargetNotSelected;" + plan.ProposedAction, plan.PreflightChecks,
            plan.RequiredPrivileges, plan.RiskLevel, plan.RollbackCapability, plan.VerificationMetrics, false, 0);
    }

    public static string ActionLabel(RecoveryAction action) => action switch
    {
        RecoveryAction.ReviewStartup => "시작 프로그램 검토", RecoveryAction.ReviewBackground => "백그라운드 부하 검토",
        RecoveryAction.ReviewUserCache => "사용자 캐시 검토", RecoveryAction.ReviewDiskSpace => "디스크 공간 검토",
        RecoveryAction.CheckWindowsComponents => "Windows 구성 요소 점검 계획", RecoveryAction.CheckUpdateState => "업데이트 상태 점검 계획",
        RecoveryAction.ReviewServiceConfiguration => "서비스 설정 검토", _ => "앱 자원 사용 재확인"
    };
    private static RecoveryAction SuggestedAction(string category) => category.Contains("MemoryPressure", StringComparison.Ordinal)
        ? RecoveryAction.ReviewBackground : RecoveryAction.ReviewApplicationLoad;
    private static bool IsSupportedCategory(string category) => category == "CpuSaturation" || category == "MemoryPressure"
        || category == "DiskIoPressure" || category.StartsWith("DiskIoPressure.", StringComparison.Ordinal);
}
