using SmartPerformanceDoctor.App.Models.SystemCare;
using SmartPerformanceDoctor.Diagnostics;

namespace SmartPerformanceDoctor.App.Services.SystemCare;

public static class PerformanceFindingAdapter
{
    public static IReadOnlyList<CareFinding> Convert(DiagnosticSnapshot snapshot) => CareFindingQualityGate.Evaluate(
        BottleneckAnalyzer.Analyze(snapshot).Select(f => new CareFinding
        {
            Id = f.FindingId, Title = f.Category, Detail = $"{f.Severity} · {f.MeasurementWindow} · {f.SampleCount} samples · {f.RecommendedNextCheck}",
            Confidence = f.Confidence, Evidence = f.Evidence + " " + string.Join(" ", f.Limitations), DetectionSource = f.SourceProvider,
            CanAutoApply = false, RiskCode = "review", RiskLabel = "측정 결과", AutoApplyBlockReason = f.RecommendedNextCheck
        }).ToArray());
}
