using SmartPerformanceDoctor.Diagnostics;
var finding=new Finding("id","InsufficientEvidence.CpuSaturation","synthetic","10s",10,"fixture",0,"Unknown",[],"repeat");
var result=ReferenceComparison.Evaluate([finding],[new("CpuSaturation",false)],true,true);
Console.WriteLine($"InsufficientEvidence={result.InsufficientEvidence};Unknown={result.Unknown};TrueNegative={result.TrueNegative}");
namespace SmartPerformanceDoctor.Diagnostics {
public record Finding(string FindingId,string Category,string Evidence,string MeasurementWindow,int SampleCount,string SourceProvider,double Confidence,string Severity,string[] Limitations,string RecommendedNextCheck);
public record Error(string Provider,string Code);
public record EtwStatistics(long? NativeEtwEventsLost,long ApplicationQueueDropped,long MalformedEvents,long EventsPendingAtStop,bool RangeReduced);
public record DiagnosticSnapshot(List<int> Samples,EtwStatistics? EtwStatistics,List<Error> Errors,double DurationSeconds);
}
