using SmartPerformanceDoctor.App.Models.SystemCare;
using SmartPerformanceDoctor.Diagnostics;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

public static class PerformanceFindingAdapter
{
    public static IReadOnlyList<CareFinding> Convert(DiagnosticSnapshot snapshot) => CareFindingQualityGate.Evaluate(
        BottleneckAnalyzer.Analyze(snapshot).Select(f => new CareFinding
        {
            Id = f.FindingId, Title = DiagnosticPresentation.Category(f.Category), Detail = $"{DiagnosticPresentation.State(f.Severity)} · {f.SampleCount}개 표본 · 자료 충족도 {f.EvidenceCoverage:P0} · 측정 품질 {f.MeasurementQuality:P0} · 진단 정확도 미검증 · 같은 작업 조건으로 재측정하세요",
            Confidence = f.Confidence, Evidence = f.Evidence + " " + string.Join(" ", f.Limitations), DetectionSource = f.SourceProvider,
            CanAutoApply = false, RiskCode = "review", RiskLabel = "측정 결과", AutoApplyBlockReason = "같은 작업 조건으로 재측정하세요. 이 화면은 자동 조치를 실행하지 않습니다."
        }).ToArray());
}
