using SmartPerformanceDoctor.App.Models;
using SmartPerformanceDoctor.App.Services;
using SmartPerformanceDoctor.Contracts.Services;
using SmartPerformanceDoctor.Contracts;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class InferenceOrchestratorTests
{
    [Fact]
    public void Analyze_ReturnsInsights_ForSystemSignals()
    {
        var orchestrator = new InferenceOrchestrator();
        var diagnoses = new[]
        {
            new IntelligenceSummary
            {
                Score = 78,
                Status = "주의",
                PlainSummary = "test",
                RootCauses =
                [
                    new RootCauseCandidate
                    {
                        Area = "system",
                        Severity = "warning",
                        Explanation = "서비스 중단",
                        Recommendation = "서비스 재시작",
                        Confidence = 0.7
                    }
                ]
            }
        };

        var result = orchestrator.Analyze(
            "system",
            diagnoses,
            ["service state stopped", "win32_service anomaly"]);

        Assert.True(result.FusedScore > 0);
        Assert.NotEmpty(result.Insights);
        Assert.NotNull(result.EnhancedIntelligence);
    }

    [Fact]
    public void BuildPlan_UsesInferenceActions_WhenProvided()
    {
        var inference = new InferenceResult(
            "system",
            70,
            "주의",
            "test",
            Array.Empty<InferenceInsight>(),
            ["dism_checkhealth", "driver_check_problem_devices"],
            null);

        var plan = RepairPlanMapper.BuildPlan("system", Array.Empty<IntelligenceSummary>(), onlyWhenIssuesFound: false, inference);
        Assert.Contains(plan, action => action.Id == "dism_checkhealth");
        Assert.DoesNotContain(plan, action => action.Id == "driver_check_problem_devices");
    }

    [Fact]
    public void Analyze_SystemScope_ExcludesDriverAudioRepairs()
    {
        var orchestrator = new InferenceOrchestrator();
        var result = orchestrator.Analyze(
            "system",
            Array.Empty<IntelligenceSummary>(),
            ["pnp problem device driver audio audiosrv stopped"]);

        Assert.All(result.RecommendedRepairActionIds, id =>
            Assert.True(ScopeRepairFilter.IsAllowedForScope(id, "system")));
        Assert.DoesNotContain(result.RecommendedRepairActionIds, id => id.Contains("driver", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.RecommendedRepairActionIds, id => id.Contains("audio", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_DoesNotCorrelateKeywordsAcrossDifferentSignalLines()
    {
        var result = new InferenceOrchestrator().Analyze(
            "system",
            Array.Empty<IntelligenceSummary>(),
            ["service inventory collected", "background process stopped"]);

        Assert.DoesNotContain(result.Insights, insight =>
            insight.Category == "local-rules" && insight.Title.Contains("서비스", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_MatchesCorrelatedKeywordsWithinOneSignalLine()
    {
        var result = new InferenceOrchestrator().Analyze(
            "system",
            Array.Empty<IntelligenceSummary>(),
            ["service audiosrv stopped unexpectedly"]);

        Assert.Contains(result.Insights, insight =>
            insight.Category == "local-rules" && insight.Title.Contains("서비스", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_LocalAiOpinionAlone_DoesNotCreateRepairActions()
    {
        var llm = new LocalLlmAnalysisResult(
            true,
            "analysis",
            [new LocalLlmInsight("참고", "추가 확인 권장", 0.99)]);

        var result = new InferenceOrchestrator().Analyze(
            "system",
            Array.Empty<IntelligenceSummary>(),
            Array.Empty<string>(),
            llm);

        Assert.Empty(result.RecommendedRepairActionIds);
    }
}
