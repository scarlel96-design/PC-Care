using System.Text.RegularExpressions;
using SmartPerformanceDoctor.App.Models;

namespace SmartPerformanceDoctor.App.Services;

public sealed record CareProgressPresentation(
    double Percent,
    string Phase,
    string Headline,
    string Flow);

/// <summary>
/// Converts engine-local progress and care steps into one monotonic, user-facing timeline.
/// A running operation never moves backwards when a module or phase changes.
/// </summary>
public sealed class CareProgressTracker
{
    private static readonly Regex PercentRegex = new(@"(?<value>\d{1,3})%", RegexOptions.Compiled);
    private readonly int _diagnosisUnits;
    private double _current;
    private int _completedDiagnosisUnits;
    private int _inferenceEvents;
    private int _optimizationEvents;
    private int _repairEvents;

    public CareProgressTracker(int diagnosisUnits = 1)
    {
        _diagnosisUnits = Math.Max(1, diagnosisUnits);
    }

    public CareProgressPresentation AdvanceScan(int rawPercent, string message)
    {
        var target = Math.Clamp(rawPercent, 0, 100);
        _current = Math.Max(_current, target);
        var (index, phase) = _current switch
        {
            < 10 => (1, "준비"),
            < 72 => (2, "검사"),
            < 96 => (3, "분석"),
            _ => (4, "결과")
        };

        return Present(phase, $"{index}/4 · {message}");
    }

    public CareProgressPresentation AdvanceUnified(CareStepResult step)
    {
        var phase = NormalizePhase(step.Phase);
        double target;
        string flow;

        switch (phase)
        {
            case "검사":
                var slice = 56d / _diagnosisUnits;
                var localPercent = ParsePercent(step.Title);
                target = localPercent.HasValue
                    ? 6 + (_completedDiagnosisUnits * slice) + (localPercent.Value / 100d * slice)
                    : 6 + (_completedDiagnosisUnits * slice) + Math.Min(slice * 0.18, 2.5);

                if (IsDiagnosisCompletion(step))
                {
                    _completedDiagnosisUnits = Math.Min(_diagnosisUnits, _completedDiagnosisUnits + 1);
                    target = 6 + (_completedDiagnosisUnits * slice);
                }

                target = Math.Min(target, 62);
                flow = $"검사 {_completedDiagnosisUnits}/{_diagnosisUnits} · {StripPercent(step.Title)}";
                break;
            case "분석":
                _inferenceEvents++;
                target = Math.Min(78, 64 + (_inferenceEvents * 4));
                flow = $"검사 완료 · 분석 {_inferenceEvents}단계 · {step.ExecutionKind}";
                break;
            case "최적화":
                _optimizationEvents++;
                target = Math.Min(87, 79 + (_optimizationEvents * 2));
                flow = $"분석 완료 · 최적화 {_optimizationEvents}단계";
                break;
            case "복구":
                _repairEvents++;
                target = Math.Min(96, 88 + (_repairEvents * 1.5));
                flow = $"복구 {_repairEvents}단계 · {step.ExecutionKind}";
                break;
            case "검증":
                target = 98;
                flow = "복구 완료 · 결과 검증";
                break;
            case "완료":
                target = 100;
                flow = "모든 처리 단계 완료";
                break;
            default:
                target = Math.Min(99, _current + 1);
                flow = step.ExecutionKind;
                break;
        }

        _current = Math.Max(_current, target);
        return Present(phase, flow);
    }

    public CareProgressPresentation Complete(string message = "모든 처리 단계 완료")
    {
        _current = 100;
        return Present("완료", message);
    }

    private CareProgressPresentation Present(string phase, string flow)
    {
        var rounded = Math.Clamp((int)Math.Round(_current), 0, 100);
        return new CareProgressPresentation(rounded, phase, $"{phase} · {rounded}%", flow);
    }

    private static int? ParsePercent(string title)
    {
        var match = PercentRegex.Match(title ?? string.Empty);
        return match.Success && int.TryParse(match.Groups["value"].Value, out var value)
            ? Math.Clamp(value, 0, 100)
            : null;
    }

    private static string StripPercent(string title) =>
        PercentRegex.Replace(title ?? string.Empty, string.Empty).Trim(' ', '·', '—', '-');

    private static bool IsDiagnosisCompletion(CareStepResult step) =>
        step.Phase.Equals("diagnosis", StringComparison.OrdinalIgnoreCase)
        && (step.Title.Contains("완료", StringComparison.Ordinal)
            || step.Title.Contains("양호", StringComparison.Ordinal)
            || step.Title.Contains("주의", StringComparison.Ordinal)
            || step.Title.Contains("오류", StringComparison.Ordinal)
            || step.Title.Contains("제한", StringComparison.Ordinal));

    private static string NormalizePhase(string phase) => phase.ToLowerInvariant() switch
    {
        "diagnosis" => "검사",
        "inference" => "분석",
        "optimization" => "최적화",
        "repair" => "복구",
        "verification" => "검증",
        "summary" => "완료",
        _ => "처리"
    };
}