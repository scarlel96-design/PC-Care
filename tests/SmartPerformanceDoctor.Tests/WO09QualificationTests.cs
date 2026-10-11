using SmartPerformanceDoctor.Diagnostics;
using Xunit;

namespace SmartPerformanceDoctor.Tests;

public sealed class WO09QualificationTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-10-11T00:00:00Z");
    private static DiagnosticSnapshot Snapshot(double cpu = 95) => new(T, 30, "Synthetic",
        Enumerable.Range(1,30).Select(i=>new PerformanceSample(i,cpu,60,40,1,0,0,0,0,0,[],new Dictionary<string,double?>(),[],null)).ToArray(),
        [],0,0,0,0,0,"Unspecified",[],0,0);
    private static IndependentTrace Trace(double cpu=95, long? lost=0, bool independent=true) => new("Microsoft.TraceProcessing", independent,lost,0,0,
        new(T,T.AddSeconds(30)),Enumerable.Range(1,30).Select(i=>new ReferencePoint(T.AddSeconds(i),cpu)).ToArray());
    private static Finding Finding(string severity="Candidate", string category="CpuSaturation") => new("perf."+category,category,"Measured", "30 seconds",20,"Synthetic",.8,severity,[],"Repeat") {EvidenceCoverage=1,MeasurementQuality=.9};

    [Fact] public void MatchingPressureCountsTruePositive() { var r=ReferenceQualification.Evaluate(Snapshot(),Trace(),new(T,T.AddSeconds(30))); Assert.Equal(1,r.Metrics.TruePositive); Assert.Equal(2,r.Metrics.Unknown); Assert.Equal(1,r.Metrics.Precision); Assert.Equal("PressureVerified",r.PressureState); }
    [Fact] public void NoPressureCountsTrueNegative() {var r=ReferenceQualification.Evaluate(Snapshot(20),Trace(20),new(T,T.AddSeconds(30)));Assert.Equal(1,r.Metrics.TrueNegative);Assert.Null(r.Metrics.Precision);}
    [Fact] public void WrongDiagnosisCountsFalsePositive() {var r=ReferenceQualification.Evaluate(Snapshot(),Trace(20),new(T,T.AddSeconds(30)));Assert.Equal(1,r.Metrics.FalsePositive);}
    [Fact] public void MissedPressureCountsFalseNegative() {var r=ReferenceQualification.Evaluate(Snapshot(20),Trace(),new(T,T.AddSeconds(30)));Assert.Equal(1,r.Metrics.FalseNegative);}
    [Theory][InlineData(1L)][InlineData(null)] public void LostOrUnknownReferenceExcludesAccuracy(long? lost) {var r=ReferenceQualification.Evaluate(Snapshot(),Trace(lost:lost),new(T,T.AddSeconds(30)));Assert.Equal("NOT TESTED",r.Status);Assert.Null(r.Metrics.Precision);}
    [Fact] public void SameParserCannotQualify() {var r=ReferenceQualification.Evaluate(Snapshot(),Trace(independent:false),new(T,T.AddSeconds(30)));Assert.Null(r.Metrics.Recall);}
    [Fact] public void LostBufferRejectsReference() {var r=ReferenceQualification.Evaluate(Snapshot(),Trace() with {LostBuffers=1},new(T,T.AddSeconds(30)));Assert.Equal("NOT TESTED",r.Status);}
    [Fact] public void CpuGapRejectsReference() {var r=ReferenceQualification.Evaluate(Snapshot(),Trace() with {CpuGaps=1},new(T,T.AddSeconds(30)));Assert.Equal("NOT TESTED",r.Status);}
    [Fact] public void MissingReferenceSamplesStayUnknown() {var r=ReferenceQualification.Evaluate(Snapshot(),Trace() with {Cpu=[]},new(T,T.AddSeconds(30)));Assert.Equal("Unknown",r.PressureState);Assert.Null(r.Metrics.Precision);}
    [Theory][InlineData(-1)][InlineData(101)][InlineData(double.NaN)] public void InvalidReferenceValuesStayUnknown(double value) {var r=ReferenceQualification.Evaluate(Snapshot(),Trace(value),new(T,T.AddSeconds(30)));Assert.Equal("Unknown",r.PressureState);}
    [Fact] public void DisjointWindowDoesNotCompare() {var r=ReferenceQualification.Evaluate(Snapshot(),Trace(),new(T.AddMinutes(1),T.AddMinutes(2)));Assert.Null(r.Window);}
    [Fact] public void CropKeepsOnlyCompleteIntervals() {var r=ReferenceQualification.Crop(Snapshot(),new(T.AddSeconds(.5),T.AddSeconds(20.5)));Assert.Equal(19,r.Samples.Count);Assert.Equal(1.5,r.Samples[0].Seconds);Assert.Empty(r.EtwContributions);}
    [Fact] public void CropRejectsBeyondCollection() {Assert.Throws<ArgumentException>(()=>ReferenceQualification.Crop(Snapshot(),new(T.AddSeconds(-1),T.AddSeconds(30))));}
    [Fact] public void MisorderedReferenceTimeStaysUnknown() {var r=ReferenceQualification.Evaluate(Snapshot(),Trace() with {Cpu=Trace().Cpu.Reverse().ToArray()},new(T,T.AddSeconds(30)));Assert.Equal("Unknown",r.PressureState);}
    [Fact] public void SpikeDoesNotProvePressure() {var tr=Trace(20) with {Cpu=Trace(20).Cpu.Select((p,i)=>i==1?p with {CpuPercent=99}:p).ToArray()};var r=ReferenceQualification.Evaluate(Snapshot(20),tr,new(T,T.AddSeconds(30)));Assert.Equal(1,r.Metrics.TrueNegative);}
    [Fact] public void ShortWindowRemainsUnqualified() {var r=ReferenceQualification.Evaluate(Snapshot(),Trace(),new(T,T.AddSeconds(8)));Assert.Equal("NOT TESTED",r.Status);}
    [Fact] public void IndependentPdhCpuSurvivesApplicationEtwLoss() {var r=ReferenceQualification.Evaluate(Snapshot() with {EventsLost=500},Trace(),new(T,T.AddSeconds(30)));Assert.Equal(1,r.Metrics.TruePositive);}

    [Fact] public void PlanHasAllRequiredSafetyFields() {var p=RecoveryPlanner.Create(Finding(),RecoveryAction.ReviewApplicationLoad);Assert.Equal("ReviewOnly",p.Eligibility);Assert.Equal("Unknown",p.ExpectedEffect);Assert.True(p.UserConsentRequired);Assert.NotEmpty(p.RollbackPlan);Assert.NotEmpty(p.VerificationMetrics);Assert.Equal("PlannedOnly",p.ExecutionState);}
    [Theory][InlineData("Unknown")][InlineData("ExpectedWorkload")][InlineData("Observation")][InlineData("InsufficientEvidence")][InlineData("ReferenceMismatch")]
    public void UnqualifiedObservationBlocksPlan(string state) {Assert.StartsWith("Blocked:",RecoveryPlanner.Create(Finding(state),RecoveryAction.ReviewApplicationLoad).Eligibility);}
    [Theory][InlineData(0)][InlineData(.79)][InlineData(double.NaN)][InlineData(2)] public void InsufficientCoverageBlocksPlan(double coverage) {var p=RecoveryPlanner.Create(Finding() with {EvidenceCoverage=coverage},RecoveryAction.ReviewApplicationLoad);Assert.StartsWith("Blocked:",p.Eligibility);}
    [Fact] public void ShortSampleCountBlocksPlan() {Assert.StartsWith("Blocked:",RecoveryPlanner.Create(Finding() with {SampleCount=1},RecoveryAction.ReviewApplicationLoad).Eligibility);}
    [Fact] public void UnsupportedActionBlocksPlan() {Assert.StartsWith("Blocked:",RecoveryPlanner.Create(Finding(),RecoveryAction.ReviewServiceConfiguration).Eligibility);}
    [Fact] public void UnsupportedCategoryBlocksPlan() {Assert.StartsWith("Blocked:",RecoveryPlanner.Create(Finding(category:"ArbitraryScript"),RecoveryAction.ReviewApplicationLoad).Eligibility);}
    [Fact] public void DryRunCannotExecute() {var r=RecoveryPlanner.DryRun(RecoveryPlanner.Create(Finding(),RecoveryAction.ReviewApplicationLoad));Assert.Equal("PreviewAvailable",r.Status);Assert.False(r.ExecutionEnabled);Assert.Equal(0,r.SystemMutations);}
    [Fact] public void MissingConsentRejectsPlan() {var p=RecoveryPlanner.Create(Finding(),RecoveryAction.ReviewApplicationLoad) with {UserConsentRequired=false};Assert.Equal("Blocked:InvalidPlan",RecoveryPlanner.DryRun(p).Status);}
    [Fact] public void FakeImprovementRejectsPlan() {var p=RecoveryPlanner.Create(Finding(),RecoveryAction.ReviewApplicationLoad) with {ExpectedEffect="50%"};Assert.Equal("Blocked:InvalidPlan",RecoveryPlanner.DryRun(p).Status);}
    [Fact] public void ExecuteStateRejectsPlan() {var p=RecoveryPlanner.Create(Finding(),RecoveryAction.ReviewApplicationLoad) with {ExecutionState="Executing"};Assert.False(RecoveryPlanner.DryRun(p).ExecutionEnabled);}
    [Fact] public void NoPressureDoesNotCreatePlan() {Assert.Empty(RecoveryPlanner.Create([Finding(category:"NoObservedPressure.CpuSaturation")]));}
    [Fact] public void MemoryEvidenceSelectsBackgroundReview() {Assert.Equal(RecoveryAction.ReviewBackground,RecoveryPlanner.Create([Finding(category:"MemoryPressure")])[0].ProposedAction);}
    [Fact] public void DiskEvidenceDoesNotInventSpaceShortage() {Assert.Equal(RecoveryAction.ReviewApplicationLoad,RecoveryPlanner.Create([Finding(category:"DiskIoPressure")])[0].ProposedAction);}
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)] public void AllActionCandidatesHaveLabels(int action) {Assert.NotEmpty(RecoveryPlanner.ActionLabel((RecoveryAction)action));Assert.False(RecoveryPlanner.DryRun(RecoveryPlanner.Create(Finding(),(RecoveryAction)action)).ExecutionEnabled);}
    [Fact] public void InvalidActionIsRejected() {Assert.Throws<ArgumentOutOfRangeException>(()=>RecoveryPlanner.Create(Finding(),(RecoveryAction)999));}

    [Fact] public void LowCpuIoConsumerIsSelected() {var all=Enumerable.Range(0,200).Select(i=>new ProcessMetric(i.ToString(),i<199?50:0,200-i,1)).ToArray();var selected=ProcessSelection.Select(all,128,[new("199",0,1000000,0,100)]);Assert.Contains(selected,p=>p.Identifier=="199");Assert.Equal(128,selected.Count);}
    [Fact] public void UnresolvedIoDoesNotJoinByPid() {var selected=ProcessSelection.Select([new("generation-A",50,10,1),new("generation-B",0,0,1)],1,[new("unresolved",0,1000,0,10)]);Assert.Equal("generation-A",selected[0].Identifier);}
    [Fact] public void NegativeIoIsExcluded() {var selected=ProcessSelection.Select([new("a",50,10,1),new("b",0,0,1)],1,[new("b",0,-1,1000,10)]);Assert.Equal("a",selected[0].Identifier);}
    [Fact] public void DiskNumberChangePreservesLocalIdentity() {var key=new byte[32];var a=PhysicalDiskMapping.Resolve("0 C:",[new(0,"stable","SATA","SSD",2)],key);var b=PhysicalDiskMapping.Resolve("4 D:",[new(4,"stable","SATA","SSD",2)],key);Assert.Equal(a.Identifier,b.Identifier);Assert.Equal("SATA SSD",a.DeviceType);}
    [Fact] public void MissingDiskIdentityStaysUnknown() {Assert.Null(PhysicalDiskMapping.Resolve("0 C:",[new(0,null,"NVMe","Unknown",1)],new byte[32]).Identifier);}
    [Fact] public void DuplicateDiskNumberStaysUnknown() {Assert.Equal("Unknown",PhysicalDiskMapping.Resolve("0 C:",[new(0,"a","SATA","SSD",1),new(0,"b","SATA","SSD",1)],new byte[32]).DeviceType);}
    [Fact] public void DiskSerialIsNotExported() {var r=PhysicalDiskMapping.Resolve("0 C:",[new(0,"secret-serial","NVMe","SSD",1)],new byte[32]);Assert.DoesNotContain("secret",r.Identifier!);Assert.Equal("NVMe SSD",r.DeviceType);}
    [Fact] public void TotalCounterIsNotDevice() {Assert.Null(PhysicalDiskMapping.Resolve("_Total",[],new byte[32]).Identifier);}
    [Fact] public void ShortLocalKeyIsRejected() {Assert.Throws<ArgumentException>(()=>PhysicalDiskMapping.Resolve("0",[],new byte[1]));}
    [Fact] public void IndependentReferenceInsufficientStateIsExcluded() {var r=ReferenceComparison.Evaluate([Finding("InsufficientEvidence")],[new("CpuSaturation",true)],true,true);Assert.Equal(1,r.InsufficientEvidence);Assert.Null(r.Precision);}
    [Fact] public void UnknownObservationIsNotNormal() {var r=ReferenceComparison.Evaluate([Finding("Unknown")],[new("CpuSaturation",false)],true,true);Assert.Equal(1,r.Unknown);Assert.Equal(0,r.TrueNegative);}
    [Fact] public void MismatchIsCountedSeparately() {var r=ReferenceComparison.Evaluate([Finding("ReferenceMismatch")],[new("CpuSaturation",true)],true,true);Assert.Equal(1,r.ReferenceMismatch);Assert.Equal(0,r.InsufficientEvidence);}
    [Fact] public void LifecycleRejectsOverlappingCollectors() {var x=new DiagnosticLifecycle();Assert.True(x.TryBegin(out var a));Assert.False(x.TryBegin(out _));Assert.True(x.Complete(a!));}
    [Fact] public void StopRequiresCleanupBeforeRestart() {var x=new DiagnosticLifecycle();x.TryBegin(out var a);x.Cancel();Assert.True(a!.Token.IsCancellationRequested);Assert.False(x.TryBegin(out _));x.Complete(a);Assert.True(x.TryBegin(out var b));x.Complete(b!);}
    [Fact] public void NavigationDiscardsStaleProgress() {var x=new DiagnosticLifecycle();x.TryBegin(out var a);x.Cancel(true);Assert.False(x.IsCurrent(a!));Assert.True(x.Complete(a!));Assert.True(x.TryBegin(out var b));Assert.True(x.IsCurrent(b!));x.Complete(b!);}
    [Fact] public void OldCompletionCannotReleaseNewCollector() {var x=new DiagnosticLifecycle();x.TryBegin(out var a);x.Complete(a!);x.TryBegin(out var b);Assert.False(x.Complete(a!));Assert.False(x.TryBegin(out _));x.Complete(b!);}
    [Fact] public void RepeatedCancellationIsIdempotent() {var x=new DiagnosticLifecycle();x.TryBegin(out var a);x.Cancel();x.Cancel();Assert.True(x.Complete(a!));Assert.False(x.Complete(a!));}
    [Fact] public void RepeatedCollectionUsesDistinctGeneration() {var x=new DiagnosticLifecycle();x.TryBegin(out var a);x.Complete(a!);x.TryBegin(out var b);Assert.NotEqual(a!.Generation,b!.Generation);Assert.False(x.IsCurrent(a));x.Complete(b);}
}
