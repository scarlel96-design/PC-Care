using SmartPerformanceDoctor.Diagnostics;
using Xunit;

namespace SmartPerformanceDoctor.Tests;
public sealed class WO10ReferenceTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-10-11T00:00:00Z");
    private static readonly MeasurementWindow Window = new(T, T.AddSeconds(10));
    [Fact] public void IdleIsNotBusy() => Assert.All(CpuSchedulingReference.Normalize([new(0,T,T.AddSeconds(10),CpuActivity.Idle)],1,Window).Bins,b=>Assert.Equal(0,b.CpuPercent));
    [Fact] public void BusyIsMeasuredPerProcessor() => Assert.All(CpuSchedulingReference.Normalize([new(0,T,T.AddSeconds(10),CpuActivity.Busy),new(1,T,T.AddSeconds(10),CpuActivity.Idle)],2,Window).Bins,b=>Assert.Equal(50,b.CpuPercent));
    [Fact] public void CrossingIntervalsAreClipped() {var x=CpuSchedulingReference.Normalize([new(0,T.AddSeconds(-1),T.AddSeconds(.25),CpuActivity.Busy),new(0,T.AddSeconds(.25),T.AddSeconds(11),CpuActivity.Idle)],1,Window);Assert.Equal(25,x.Bins[0].CpuPercent);Assert.Equal(0,x.Bins[1].CpuPercent);}
    [Fact] public void MissingProcessorIsNotIdle() {var x=CpuSchedulingReference.Normalize([new(0,T,T.AddSeconds(10),CpuActivity.Busy)],2,Window);Assert.All(x.Bins,b=>{Assert.Null(b.CpuPercent);Assert.Equal(1,b.MissingSeconds);});}
    [Fact] public void UnknownThreadIsNotBusy() => Assert.All(CpuSchedulingReference.Normalize([new(0,T,T.AddSeconds(10),CpuActivity.Unknown)],1,Window).Bins,b=>Assert.Null(b.CpuPercent));
    [Fact] public void OverlapIsRejectedWithoutClamping() {var x=CpuSchedulingReference.Normalize([new(0,T,T.AddSeconds(10),CpuActivity.Busy),new(0,T,T.AddSeconds(10),CpuActivity.Busy)],1,Window);Assert.All(x.Bins,b=>{Assert.Null(b.CpuPercent);Assert.Equal(1,b.OverlapSeconds);});}
    [Theory][InlineData(-1)][InlineData(2)] public void InvalidProcessorRejectsQualification(int cpu) {var x=CpuSchedulingReference.Normalize([new(cpu,T,T.AddSeconds(10),CpuActivity.Busy)],1,Window);Assert.Equal(1,x.InvalidIntervals);Assert.All(x.Bins,b=>Assert.Null(b.CpuPercent));}
    [Fact] public void ReversedTimeIsRejected() {var x=CpuSchedulingReference.Normalize([new(0,T.AddSeconds(2),T,CpuActivity.Busy)],1,Window);Assert.Equal(1,x.InvalidIntervals);}
    [Fact] public void ShortWindowIsRejected() => Assert.Throws<ArgumentException>(()=>CpuSchedulingReference.Normalize([],1,new(T,T.AddSeconds(2))));
    [Fact] public void EmptyInputHasUnknownUtilization() => Assert.All(CpuSchedulingReference.Normalize([],1,Window).Bins,b=>Assert.Null(b.CpuPercent));
    [Fact] public void InputOrderDoesNotChangeResult() {var a=new CpuExecutionInterval(0,T,T.AddSeconds(5),CpuActivity.Idle);var b=new CpuExecutionInterval(0,T.AddSeconds(5),T.AddSeconds(10),CpuActivity.Busy);Assert.Equal(CpuSchedulingReference.Normalize([a,b],1,Window).Bins,CpuSchedulingReference.Normalize([b,a],1,Window).Bins);}
    [Fact] public void ProductionInsufficientFindingIsCountedSeparately()
    {
        var snapshot=new DiagnosticSnapshot(T,30,"Synthetic",[],[],0,0,0,0,0,"Unspecified",[],0,0);
        var findings=BottleneckAnalyzer.Analyze(snapshot);
        Assert.Contains(findings,f=>f.Category=="InsufficientEvidence.CpuSaturation" && f.Severity=="Unknown");
        var result=ReferenceComparison.Evaluate(findings,[new("CpuSaturation",false)],true,true);
        Assert.Equal(1,result.InsufficientEvidence);Assert.Equal(0,result.Unknown);Assert.Equal(0,result.TrueNegative);Assert.Null(result.Precision);
    }
    [Fact] public void OrdinaryUnknownRemainsUnknown()
    {
        var f=new Finding("id","CpuSaturation","e","10 seconds",10,"synthetic",0,"Unknown",[],"repeat");
        var r=ReferenceComparison.Evaluate([f],[new("CpuSaturation",false)],true,true);Assert.Equal(1,r.Unknown);Assert.Equal(0,r.InsufficientEvidence);
    }
    [Fact] public void UnqualifiedReferenceKeepsAccuracyNull()
    {
        var snapshot=new DiagnosticSnapshot(T,30,"Synthetic",[],[],0,0,0,0,0,"Unspecified",[],0,0);
        var reference=new IndependentTrace("Microsoft.TraceProcessing",true,0,0,0,Window,[]) {SemanticsQualified=false};
        var result=ReferenceQualification.Evaluate(snapshot,reference,Window);
        Assert.Contains("ReferenceSemanticsNotQualified",result.Exclusions);
        Assert.Null(result.Metrics.Precision);Assert.Null(result.Metrics.Recall);
    }
}
