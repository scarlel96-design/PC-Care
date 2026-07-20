using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class CareProgressTrackerTests
{
    [Fact]
    public void ScanProgress_IsMonotonic_AndCombinesPhaseWithPercent()
    {
        var tracker = new CareProgressTracker();
        var values = new[]
        {
            tracker.AdvanceScan(5, "준비"),
            tracker.AdvanceScan(48, "캐시 검사"),
            tracker.AdvanceScan(35, "늦게 도착한 이벤트"),
            tracker.AdvanceScan(92, "결과 분석"),
            tracker.Complete()
        };

        Assert.Equal(values.Select(v => v.Percent).OrderBy(v => v), values.Select(v => v.Percent));
        Assert.All(values, value => Assert.Contains($"{value.Percent:0}%", value.Headline));
        Assert.Equal("완료", values[^1].Phase);
    }

    [Fact]
    public void UnifiedProgress_DoesNotRegress_WhenNextModuleStartsAtZero()
    {
        var tracker = new CareProgressTracker(diagnosisUnits: 2);
        var firstModule = tracker.AdvanceUnified(Step("diagnosis", "시스템 — 90%"));
        var nextModule = tracker.AdvanceUnified(Step("diagnosis", "시스템 점검 완료"));
        var resetEvent = tracker.AdvanceUnified(Step("diagnosis", "오디오 — 5%"));
        var analysis = tracker.AdvanceUnified(Step("inference", "분석 완료"));

        Assert.True(nextModule.Percent >= firstModule.Percent);
        Assert.True(resetEvent.Percent >= nextModule.Percent);
        Assert.True(analysis.Percent >= resetEvent.Percent);
        Assert.Equal("분석", analysis.Phase);
    }

    private static CareStepResult Step(string phase, string title) =>
        new(phase, title, "detail", "점검", true);
}