namespace SmartPerformanceDoctor.Diagnostics;

public sealed record ReferenceObservation(string Category, bool? Present);
public sealed record AccuracyMetrics(string Status, int TruePositive, int FalsePositive, int FalseNegative,
    int TrueNegative, int Unknown, int InsufficientEvidence, int ReferenceMismatch, double? Precision, double? Recall);
public static class ReferenceComparison
{
    public static AccuracyMetrics Evaluate(IReadOnlyList<Finding> findings, IReadOnlyList<ReferenceObservation> reference,
        bool independentlyCollected, bool windowAligned)
    {
        if (!independentlyCollected || !windowAligned || reference.Count == 0)
            return new("NOT TESTED", 0, 0, 0, 0, reference.Count, 0, 0, null, null);
        if (reference.Select(r => r.Category).Distinct().Count() != reference.Count) throw new ArgumentException("DuplicateReferenceCategory");
        int tp = 0, fp = 0, fn = 0, tn = 0, unknown = 0, insufficient = 0, mismatch = 0;
        foreach (var r in reference)
        {
            if (r.Present is null) { unknown++; continue; }
            var f = findings.Where(f => f.Category == r.Category || f.Category == "NoObservedPressure." + r.Category
                || f.Category == "Transient." + r.Category || f.Category == "InsufficientEvidence." + r.Category).ToArray();
            if (f.Length != 1) { mismatch++; continue; }
            if (f[0].Category.StartsWith("InsufficientEvidence.", StringComparison.Ordinal)
                || f[0].Severity == "InsufficientEvidence") { insufficient++; continue; }
            if (f[0].Severity == "Unknown") { unknown++; continue; }
            if (f[0].Severity == "ReferenceMismatch") { mismatch++; continue; }
            var present = f[0].Category == r.Category;
            if (present && r.Present.Value) tp++;
            else if (present) fp++;
            else if (r.Present.Value) fn++;
            else tn++;
        }
        return new("ControlledReferenceOnly", tp, fp, fn, tn, unknown, insufficient, mismatch,
            tp + fp > 0 ? (double)tp / (tp + fp) : null, tp + fn > 0 ? (double)tp / (tp + fn) : null);
    }
}

public static class DiagnosticPresentation
{
    public static string State(string state) => state switch
    {
        "Candidate" => "병목 후보 · 재측정 필요", "ExpectedWorkload" => "예상된 고부하 작업",
        "ConfirmedObservation" => "관측 현상 확인", "Unknown" => "측정 정보 부족",
        "InsufficientEvidence" => "판단 근거 부족", "ReferenceMismatch" => "참조 측정과 불일치",
        _ => "측정 관찰"
    };
    public static string Category(string category)
    {
        var subject = category.Contains("CpuSaturation") ? "CPU 부하" : category.Contains("MemoryPressure") ? "메모리 압박" :
            category.Contains("DiskIoPressure") ? "디스크 응답 지연" : "프로세스 자원 기여";
        return category.StartsWith("InsufficientEvidence.") ? subject + " · 판단 근거 부족" :
            category.StartsWith("NoObservedPressure.") ? subject + " · 측정 구간 내 지속 압박 없음" :
            category.StartsWith("Transient.") ? subject + " · 일시적 상승" : subject;
    }
    public static string Coverage(DiagnosticSnapshot snapshot)
    {
        var stats = snapshot.EtwStatistics;
        var loss = stats is null ? "이벤트 손실 측정 정보 없음" :
            $"ETW 내부 손실 {stats.NativeEtwEventsLost?.ToString() ?? "미측정"} · 수집 큐 손실 {stats.ApplicationQueueDropped} · 잘못된 이벤트 {stats.MalformedEvents} · 종료 대기 {stats.EventsPendingAtStop}";
        var permission = snapshot.Errors.Any(e => e.Code == "PermissionDenied") ? " · 권한 부족: 허용된 시스템 카운터만 측정" : "";
        var reduced = stats?.RangeReduced == true ? " · 이벤트 손실로 ETW 수집 중단, 시스템 카운터 측정 계속" : "";
        return $"{snapshot.Samples.Count}개 표본 · {snapshot.DurationSeconds:F1}초 · {loss}{permission}{reduced} · 온도·클럭·근본 원인 미확인";
    }
}
