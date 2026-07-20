using SmartPerformanceDoctor.App.Models.SystemCare;
using SmartPerformanceDoctor.App.Services.SystemCare;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class CareFindingQualityGateTests
{
    [Fact]
    public void ReviewFinding_IsNeverAutomaticallyApplied()
    {
        var finding = Finding("opt.visual", "review", canAutoApply: true);

        var result = Assert.Single(CareFindingQualityGate.Evaluate([finding]));

        Assert.False(result.CanAutoApply);
        Assert.NotEmpty(result.AutoApplyBlockReason);
    }

    [Fact]
    public void DnsFlush_RequiresCorrelatedDnsFailure()
    {
        var flush = Finding("opt.dns_flush", "safe", canAutoApply: false);
        var withoutFailure = Assert.Single(CareFindingQualityGate.Evaluate([flush]));
        var withFailure = CareFindingQualityGate.Evaluate([
            flush,
            Finding("net.dns_resolve", "review", canAutoApply: false)
        ]).Single(f => f.Id == "opt.dns_flush");

        Assert.False(withoutFailure.CanAutoApply);
        Assert.True(withFailure.CanAutoApply);
    }

    [Fact]
    public void ProbeError_IsUnavailable_NotAHealthProblem()
    {
        var result = Assert.Single(CareFindingQualityGate.Evaluate([
            Finding("disk.smart.error", "review", canAutoApply: true)
        ]));

        Assert.Equal("unavailable", result.RiskCode);
        Assert.False(result.CanAutoApply);
        Assert.Equal(100, CareHealthScorer.Score([result]).Score);
    }

    [Fact]
    public void DuplicateEvidence_IsCollapsed()
    {
        var path = Path.GetTempPath();
        var results = CareFindingQualityGate.Evaluate([
            Finding("disk.temp", "safe", true, path),
            Finding("disk.temp", "safe", true, path)
        ]);

        Assert.Single(results);
    }

    private static CareFinding Finding(string id, string risk, bool canAutoApply, string? path = null) => new()
    {
        Id = id,
        Title = id,
        Detail = "test evidence",
        RiskLabel = risk,
        RiskCode = risk,
        CanAutoApply = canAutoApply,
        TargetPath = path,
        Confidence = 0.95
    };
}