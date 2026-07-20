namespace SmartPerformanceDoctor.SecurityLab.Policy;

public enum LabActionKind
{
    VaultUnlock,
    VaultImport,
    VaultExport,
    VaultCryptoShred,
    SecureDeleteDryRun,
    SecureDeleteExecute,
    MigrateDryRun,
    MigrateExecute
}

public sealed class LabPolicyRequest
{
    public required LabActionKind Kind { get; init; }
    public bool UserConfirmed { get; init; }
    public bool DryRunCompleted { get; init; }
    public string? ConfirmPhrase { get; init; }
    public int TargetCount { get; init; }
    public bool SourceEqualsDestination { get; init; }
}

public sealed class LabPolicyDecision
{
    public bool Allowed { get; init; }
    public string Reason { get; init; } = "";
    public bool RequiresAudit { get; init; }
}

/// <summary>Final gate for destructive / sensitive lab actions (no AI override).</summary>
public static class LabPolicyEngine
{
    public const string DestroyPhrase = ShredNext.LabShredPolicy.IrreversiblePhrase;

    public static LabPolicyDecision Evaluate(LabPolicyRequest req) => req.Kind switch
    {
        LabActionKind.SecureDeleteExecute => EvaluateDestroy(req, "보안 삭제"),
        LabActionKind.VaultCryptoShred => EvaluateDestroy(req, "금고 crypto-shred", requirePhrase: false),
        LabActionKind.MigrateExecute when req.SourceEqualsDestination =>
            Deny("마이그레이션 대상 경로가 소스와 동일합니다."),
        LabActionKind.MigrateExecute when !req.UserConfirmed =>
            Deny("마이그레이션 실행은 사용자 확인이 필요합니다."),
        LabActionKind.MigrateExecute =>
            Allow("re-import 마이그레이션 허가", audit: true),
        LabActionKind.SecureDeleteDryRun or LabActionKind.MigrateDryRun =>
            Allow("dry-run 허가", audit: true),
        LabActionKind.VaultUnlock or LabActionKind.VaultImport or LabActionKind.VaultExport =>
            Allow("표준 금고 작업", audit: true),
        _ => Deny("알 수 없는 작업")
    };

    private static LabPolicyDecision EvaluateDestroy(
        LabPolicyRequest req,
        string label,
        bool requirePhrase = true)
    {
        if (!req.DryRunCompleted && req.Kind == LabActionKind.SecureDeleteExecute)
        {
            return Deny($"{label}: dry-run 이후만 실행 가능");
        }

        if (req.TargetCount <= 0 && req.Kind == LabActionKind.SecureDeleteExecute)
        {
            return Deny($"{label}: 대상 없음");
        }

        if (!req.UserConfirmed)
        {
            return Deny($"{label}: 사용자 확인 필요");
        }

        if (requirePhrase
            && !string.Equals(req.ConfirmPhrase?.Trim(), DestroyPhrase, StringComparison.Ordinal)
            && !ShredNext.LabShredPolicy.IsConfirmPhraseValid(req.ConfirmPhrase ?? ""))
        {
            return Deny($"{label}: 확인 문구 불일치");
        }

        return Allow($"{label} 허가", audit: true);
    }

    private static LabPolicyDecision Allow(string reason, bool audit) =>
        new() { Allowed = true, Reason = reason, RequiresAudit = audit };

    private static LabPolicyDecision Deny(string reason) =>
        new() { Allowed = false, Reason = reason, RequiresAudit = true };
}
