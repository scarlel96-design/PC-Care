namespace SmartPerformanceDoctor.App.Services.Commercial;

public sealed record SecureDeleteItemResult(string Status, bool PartialWritePossible);
public sealed record SecureDeleteExecutionResult(int Deleted, int Failed, string AuditPath, bool AuditValid,
    string Status, IReadOnlyList<SecureDeleteItemResult> Items)
{
    public string StatusLabel => Status switch
    {
        "Completed" => "완료",
        "Cancelled" => "취소됨",
        "PartiallyCompleted" => "일부 완료",
        _ => "실패 · 결과 확인 필요"
    };
}
