using SmartPerformanceDoctor.Diagnostics;
using Microsoft.Diagnostics.Tracing.Parsers;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

[CollectionDefinition("WO08Collector", DisableParallelization = true)]
public sealed class WO08CollectorCollection { }

[Collection("WO08Collector")]
public sealed class WO08AccuracyTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-10-11T00:00:00Z");
    private static DiagnosticSnapshot Fixture(double cpu = 95, long lost = 0) => new(T, 30, "Synthetic",
        Enumerable.Range(1,30).Select(i => new PerformanceSample(i,cpu,60,40,1,0,0,0,0,0,[],new Dictionary<string,double?>(),[],null)).ToArray(),
        [], 100, lost, 0, 0, 0, "Unspecified", [], 0, 0);
    [Fact] public void LossKindsAreDisjoint()
    {
        var b = new EventBuffer(32);
        for (int i=0;i<40;i++) b.Offer(new(0,1,false,1,false));
        b.Offer(new(0,-1,false,1,false));
        Assert.Equal(32,b.Accepted); Assert.Equal(8,b.QueueDropped); Assert.Equal(1,b.Malformed);
        Assert.Equal(b.Received,b.Accepted+b.QueueDropped+b.Malformed);
        Assert.Equal(32,b.Pending); b.Drain(); Assert.Equal(32,b.Processed); Assert.Equal(0,b.Pending);
    }
    [Theory] [InlineData(double.NaN)] [InlineData(-1)] [InlineData(double.PositiveInfinity)]
    public void MalformedTimeDoesNotEnterQueue(double seconds)
    { var b = new EventBuffer(32); b.Offer(new(0,1,false,1,false,Seconds:seconds)); Assert.Equal(1,b.Malformed); Assert.Equal(0,b.QueueDropped); }
    [Fact] public async Task ContinuousDrainPreservesAccounting()
    {
        var b = new EventBuffer(4096); var producer=Task.Run(()=> {for(int i=0;i<10000;i++) b.Offer(new(0,1,false,1,false));});
        while(!producer.IsCompleted) { b.Drain(); await Task.Yield(); }
        await producer; while(b.Pending>0)b.Drain();
        Assert.Equal(10000,b.Processed+b.QueueDropped); Assert.True(b.MaxDelayMilliseconds>=0);
    }
    [Fact] public void GeneralExcludesHighVolumeProviders()
    { var k=EtwCollector.Keywords("General"); Assert.False(k.HasFlag(KernelTraceEventParser.Keywords.ContextSwitch)); Assert.False(k.HasFlag(KernelTraceEventParser.Keywords.Profile)); Assert.True(k.HasFlag(KernelTraceEventParser.Keywords.Process)); }
    [Fact] public void DetailedIncludesOptionalProviders() => Assert.True(EtwCollector.Keywords("Detailed").HasFlag(KernelTraceEventParser.Keywords.ContextSwitch));
    [Fact] public void LowOverheadExcludesDisk() => Assert.False(EtwCollector.Keywords("LowOverhead").HasFlag(KernelTraceEventParser.Keywords.DiskIO));
    [Fact] public void UnknownProfileRejected() => Assert.Throws<ArgumentOutOfRangeException>(()=>(new SamplingPolicy { EtwProfile="Injected" }).Validate());
    [Fact] public void SnapshotAndEventsShareIdentity()
    { var r=new ProcessIdentityRegistry(); var id=r.ObserveSnapshot(40,T,T.AddSeconds(1)); Assert.Equal(id,r.Resolve(40,T.AddSeconds(2))); Assert.Equal(id,r.ObserveSnapshot(40,T,T.AddSeconds(3))); }
    [Fact] public void ReusedPidHasDifferentLifetime()
    { var r=new ProcessIdentityRegistry();var a=r.Start(40,T);r.Stop(40,T.AddSeconds(2));var b=r.Start(40,T.AddSeconds(3)); Assert.NotEqual(a,b);Assert.Equal(a,r.Resolve(40,T.AddSeconds(1)));Assert.Null(r.Resolve(40,T.AddSeconds(2)));Assert.Equal(b,r.Resolve(40,T.AddSeconds(4))); }
    [Fact] public void UnknownLifetimeIsNotInvented() => Assert.Null(new ProcessIdentityRegistry().Resolve(40,T));
    [Fact] public void SaltIsSessionLocal()
    { Assert.NotEqual(new ProcessIdentityRegistry().Start(40,T),new ProcessIdentityRegistry().Start(40,T)); }
    [Fact] public void ExportDoesNotContainPid()
    { var r=new ProcessIdentityRegistry();r.Start(123456,T);var json=System.Text.Json.JsonSerializer.Serialize(r.Export());Assert.DoesNotContain("123456",json);Assert.DoesNotContain("Pid",json); }
    [Fact] public void FutureCreationTimeRejected() => Assert.Null(new ProcessIdentityRegistry().ObserveSnapshot(40,T.AddSeconds(2),T));
    [Fact] public void DelayedStopDoesNotCloseNewGeneration()
    { var r=new ProcessIdentityRegistry();r.Start(40,T);var b=r.Start(40,T.AddSeconds(3));r.Stop(40,T.AddSeconds(2));Assert.Equal(b,r.Resolve(40,T.AddSeconds(4))); }
    [Fact] public void ReverseStartIsRejected()
    { var r=new ProcessIdentityRegistry();var id=r.Start(40,T.AddSeconds(3));Assert.Null(r.Start(40,T));Assert.Equal(id,r.Resolve(40,T.AddSeconds(4))); }
    [Fact] public void ConflictingSnapshotCannotResolveOverlap()
    { var r=new ProcessIdentityRegistry();r.Start(40,T.AddSeconds(1));r.ObserveSnapshot(40,T,T.AddSeconds(2));Assert.Null(r.Resolve(40,T.AddSeconds(1.5)));Assert.All(r.Export(),x=>Assert.True(x.EndedUtc is null || x.EndedUtc>=x.StartedUtc)); }
    [Fact] public void LifetimeBudgetIsBounded()
    { var r=new ProcessIdentityRegistry();for(int i=0;i<4097;i++)r.Start(i,T);Assert.True(r.Truncated);Assert.Equal(4096,r.Export().Count); }
    [Fact] public void HighCpuBeyondEnumerationLimitIsSelected()
    { var p=Enumerable.Range(0,200).Select(i=>new ProcessMetric("p"+i,i==199?90:0,1,1));Assert.Contains(ProcessSelection.Select(p,128),x=>x.Identifier=="p199"); }
    [Fact] public void CpuAndMemoryLeadersAreIncluded()
    { var selected=ProcessSelection.Select([new("cpu",90,1,1),new("memory",0,10000,1),new("other",1,2,1)],2);Assert.Equal(2,selected.Count);Assert.Contains(selected,x=>x.Identifier=="memory");Assert.Contains(selected,x=>x.Identifier=="cpu"); }
    [Fact] public void SelectionIsEnumerationIndependent()
    { var p=Enumerable.Range(0,200).Select(i=>new ProcessMetric("p"+i,i,200-i,1)).ToArray();Assert.Equal(ProcessSelection.Select(p,128),ProcessSelection.Select(p.Reverse(),128)); }
    [Fact] public void DiskInstancesRemainSeparate()
    { var d=PerDiskNormalizer.From(new Dictionary<string,double?> { ["perdisk.latency:0 C:"]=.001,["perdisk.latency:1 D:"]=.04 },"salt");Assert.Equal(2,d.Count);Assert.Contains(d,x=>x.LatencyMilliseconds==40);Assert.All(d,x=>Assert.Equal("Unknown",x.DeviceType)); }
    [Fact] public void DiskInvalidValueRemainsUnknown()
    { var d=PerDiskNormalizer.From(new Dictionary<string,double?> { ["perdisk.latency:disk"]=-1 },"salt");Assert.Null(Assert.Single(d).LatencyMilliseconds); }
    [Fact] public void DiskIdentityIsNotCrossSessionHardwareIdentity()
    { var values=new Dictionary<string,double?> { ["perdisk.latency:0 C:"]=.001 };Assert.NotEqual(PerDiskNormalizer.From(values,"a")[0].Identifier,PerDiskNormalizer.From(values,"b")[0].Identifier); }
    [Theory] [InlineData("different","same")] [InlineData("disk","different")]
    public void CalibrationRejectsDifferentConditions(string id,string workload)
    { var a=new DiskBaseline("disk","same",new(1,2,10),10);Assert.Equal("Unknown",DiskCalibration.Compare(a,new(id,workload,new(2,3,10),10)).Status); }
    [Fact] public void SparseCalibrationRejected()
    { var a=new DiskBaseline("disk","same",new(1,2,2),2);Assert.Equal("InsufficientEvidence",DiskCalibration.Compare(a,a).Status); }
    [Fact] public void CalibrationReportsObservationOnly()
    { var a=new DiskBaseline("disk","same",new(1,2,10),10);Assert.Equal(1,DiskCalibration.Compare(a,a with { Latency=new(2,3,10) }).MedianChangeMilliseconds); }
    [Fact] public void EtwLossDoesNotInvalidateIndependentCpu()
    { var a=BottleneckAnalyzer.Analyze(Fixture()).Single(f=>f.Category=="CpuSaturation");var b=BottleneckAnalyzer.Analyze(Fixture(lost:1000)).Single(f=>f.Category=="CpuSaturation");Assert.Equal(a.MeasurementQuality,b.MeasurementQuality);Assert.Null(b.DiagnosticAccuracy);Assert.Null(b.Reproducibility); }
    [Theory] [InlineData(false,true)] [InlineData(true,false)]
    public void MissingIndependentReferenceCannotProduceAccuracy(bool independent,bool aligned)
    { var a=ReferenceComparison.Evaluate(BottleneckAnalyzer.Analyze(Fixture()),[new("CpuSaturation",true)],independent,aligned);Assert.Equal("NOT TESTED",a.Status);Assert.Null(a.Precision);Assert.Null(a.Recall); }
    [Fact] public void ControlledReferenceCountsFalsePositive()
    { var a=ReferenceComparison.Evaluate(BottleneckAnalyzer.Analyze(Fixture()),[new("CpuSaturation",false)],true,true);Assert.Equal(1,a.FalsePositive);Assert.Equal(0,a.Precision);Assert.Null(a.Recall); }
    [Fact] public void ControlledReferenceCountsFalseNegative()
    { var a=ReferenceComparison.Evaluate(BottleneckAnalyzer.Analyze(Fixture(10)),[new("CpuSaturation",true)],true,true);Assert.Equal(1,a.FalseNegative);Assert.Equal(0,a.Recall); }
    [Fact] public void MissingReferenceCategoryIsMismatch()
    { var a=ReferenceComparison.Evaluate([], [new("CpuSaturation",true)],true,true);Assert.Equal(1,a.ReferenceMismatch);Assert.Null(a.Precision); }
    [Fact] public void UnknownTruthIsNotFalseNegative()
    { var a=ReferenceComparison.Evaluate([], [new("CpuSaturation",null)],true,true);Assert.Equal(1,a.Unknown);Assert.Equal(0,a.FalseNegative); }
    [Fact] public void UiDoesNotCallUnknownNormal() => Assert.Equal("측정 정보 부족",DiagnosticPresentation.State("Unknown"));
    [Fact] public void UiShowsSeparateLossFields()
    { var s=Fixture() with {EtwStatistics=new(null,2,3,10,10,0,"VerifiedAbsent","Unknown","General",false,0,0,8,64,128,170,new Dictionary<string,long>())};var text=DiagnosticPresentation.Coverage(s);Assert.Contains("ETW 내부 손실 미측정",text);Assert.Contains("수집 큐 손실 2",text);Assert.Contains("잘못된 이벤트 3",text); }
    [Fact] public async Task DisabledEtwDoesNotInventHardFaults()
    { var s=await new PerformanceCollector().CollectAsync(new SamplingPolicy(DurationSeconds:2){EtwProfile="Disabled"},CancellationToken.None);Assert.All(s.Samples,p=>{Assert.Null(p.HardFaults);Assert.Null(p.ContextSwitches);});Assert.Equal("DisabledByPolicy",s.EtwStatistics!.LossMeasurementStatus); }
    [Theory] [InlineData("cpu",16,1,64)] [InlineData("cpu",10,5,64)] [InlineData("memory",10,1,129)] [InlineData("delete",10,1,64)]
    public void WorkloadCannotExceedBudget(string mode,int seconds,int threads,int memory) => Assert.Throws<ArgumentOutOfRangeException>(()=>new WorkloadPolicy(mode,seconds,threads,memory).Validate());
}
