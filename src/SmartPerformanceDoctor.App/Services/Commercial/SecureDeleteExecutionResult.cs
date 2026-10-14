namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed record SecureDeleteItemResult(string Status, bool PartialWritePossible)
{
    public int TargetIndex { get; init; }
    public bool MutationStarted { get; init; }
}
public sealed record SecureDeleteExecutionResult(int Deleted, int Failed, string AuditPath, bool AuditValid,
    string Status, IReadOnlyList<SecureDeleteItemResult> Items)
{
    public bool WasCancellationRequested { get; init; }
    public bool AnyMutationStarted { get; init; }
    public int CompletedTargets => Items.Count(i => i.Status == "Completed");
    public int IncompleteTargets { get; init; }
    public bool AuditVerified => AuditValid;
    public bool RequiresManualReview { get; init; }
    public string JournalPath { get; init; } = "";
    public string StatusLabel => Status switch
    {
        "Completed" => "완료",
        "Cancelled" => "취소됨",
        "PartiallyCompleted" => "일부 완료",
        _ => "실패 · 결과 확인 필요"
    };
}
