namespace SmartPerformanceDoctor.App.Services.Commercial;

/// <summary>
/// Combines all non-destructive checks required before the WinPE-only executor can be considered.
/// This is an interlock, not an executor. It cannot itself issue a storage command.
/// </summary>
public sealed class StoragePurgePreExecutionGate
{
    public PurgePreExecutionAssessment Assess(
        StorageSanitizeDryRunResult plan,
        PurgeAuthorizationDecision authorization,
        PurgeReadOnlyInspection? inspection,
        NvmeIdentifyReadOnlyEvidence? identify,
        NvmeCommandEffectsReadOnlyEvidence? effects,
        NvmeSanitizeStatusReadOnlyEvidence? sanitizeStatus,
        DateTimeOffset now)
    {
        var blocks = new List<string>();
        var fingerprint = DeviceFingerprint.Create(plan.Device);
        if (!plan.Device.IsResolved) blocks.Add("물리 디스크 식별이 완료되지 않았습니다.");
        if (!authorization.AuthorizedForFutureExecutor || !DeviceFingerprint.FixedTimeEquals(fingerprint, authorization.DeviceFingerprint)) blocks.Add("현재 장치에 결속된 유효한 일회용 승인이 없습니다.");
        if (!plan.IsWinPe) blocks.Add("장치 Purge의 미래 실행기는 WinPE에서만 후보가 될 수 있습니다.");
        if (plan.BlockingReasons.Count > 0) blocks.AddRange(plan.BlockingReasons);
        if (inspection?.IsFresh(now) != true || !DeviceFingerprint.FixedTimeEquals(fingerprint, inspection.DeviceFingerprint)) blocks.Add("5분 이내의 일치하는 읽기 전용 장치 재식별 증적이 없습니다.");

        var crossCheck = NvmePurgeCapabilityCrossCheck.Evaluate(plan, identify, effects, now);
        if (!crossCheck.IsCurrent) blocks.Add("Identify와 Command Effects Log의 최신·동일 장치 증적이 모두 필요합니다.");
        if (!crossCheck.SanitizeCommandSupported) blocks.Add("NVMe Command Effects Log가 Sanitize(84h) 지원을 확인하지 못했습니다.");
        if (!crossCheck.CryptoEraseCrossValidated && !crossCheck.BlockEraseCrossValidated && !crossCheck.OverwriteCrossValidated) blocks.Add("교차 검증된 Sanitize 방식이 없습니다.");
        if (sanitizeStatus?.IsCurrentFor(plan, now) != true) blocks.Add("5분 이내의 NVMe Sanitize Status 증적이 없습니다.");
        else if (sanitizeStatus.State == NvmeSanitizeOperationState.InProgress) blocks.Add("이미 NVMe Sanitize 작업이 진행 중입니다.");
        else if (sanitizeStatus.State == NvmeSanitizeOperationState.Failed) blocks.Add("이전 NVMe Sanitize 작업이 실패 상태입니다. 수동 점검이 필요합니다.");

        return new PurgePreExecutionAssessment
        {
            DeviceFingerprint = fingerprint,
            IsEligibleForFutureExecutor = blocks.Count == 0,
            ExecutionEnabled = false,
            CrossCheck = crossCheck,
            BlockingReasons = blocks,
            Message = blocks.Count == 0
                ? "모든 비파괴 사전 검증을 통과했습니다. WinPE 실행기에서 별도의 일회용 승인과 최종 확인이 필요합니다."
                : "미래 실행기 후보에서 차단되었습니다. " + string.Join(" ", blocks)
        };
    }
}

public sealed class PurgePreExecutionAssessment
{
    public string DeviceFingerprint { get; init; } = "";
    public bool IsEligibleForFutureExecutor { get; init; }
    public bool ExecutionEnabled { get; init; }
    public NvmePurgeCapabilityCrossCheck CrossCheck { get; init; } = new();
    public IReadOnlyList<string> BlockingReasons { get; init; } = Array.Empty<string>();
    public string Message { get; init; } = "";
}