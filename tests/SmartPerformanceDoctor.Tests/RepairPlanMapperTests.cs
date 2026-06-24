using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.Contracts;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class RepairPlanMapperTests
{
    [Fact]
    public void BuildPlan_ReturnsDriverActions_ForDriverScope()
    {
        var plan = RepairPlanMapper.BuildPlan("driver", SampleDiagnosis(score: 70), onlyWhenIssuesFound: true);
        Assert.Contains(plan, action => action.Id == "driver_check_problem_devices");
        Assert.Contains(plan, action => action.Id == "pnputil_scan_devices");
    }

    [Fact]
    public void BuildPlan_ReturnsEmpty_WhenHealthyAndOnlyWhenIssues()
    {
        var plan = RepairPlanMapper.BuildPlan("quick", SampleDiagnosis(score: 95, status: "ok"), onlyWhenIssuesFound: true);
        Assert.Empty(plan);
    }

    [Fact]
    public void BuildPlan_ReturnsAudioActions_ForAudioScope()
    {
        var plan = RepairPlanMapper.BuildPlan("audio", SampleDiagnosis(score: 80), onlyWhenIssuesFound: false);
        Assert.Contains(plan, action => action.Id == "audio_scan_devices");
    }

    private static IReadOnlyList<IntelligenceSummary> SampleDiagnosis(int score, string status = "warning") =>
    [
        new IntelligenceSummary
        {
            Score = score,
            Status = status,
            PlainSummary = "test"
        }
    ];
}