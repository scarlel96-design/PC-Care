using SmartPerformanceDoctor.Diagnostics;
using System.Text.Json;
var findings = new[] {"Candidate", "Unknown", "ExpectedWorkload", "InsufficientEvidence"}.Select(s => new Finding("synthetic."+s,"CpuSaturation","Synthetic measured values","30 seconds",20,"Synthetic",0.8,s,[],"Repeat"){EvidenceCoverage=1,MeasurementQuality=.9}).ToArray();
var reports=findings.Select(f => {var p=RecoveryPlanner.Create(f,RecoveryAction.ReviewApplicationLoad); return new {state=f.Severity,plan=p,dryRun=RecoveryPlanner.DryRun(p)};}).ToArray();
File.WriteAllText(args[0],JsonSerializer.Serialize(reports,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"DRY_RUN_COMPLETED plans={reports.Length};preview={reports.Count(r=>r.dryRun.Status=="PreviewAvailable")};blocked={reports.Count(r=>r.dryRun.Status.StartsWith("Blocked:"))};executionEnabled={reports.Any(r=>r.dryRun.ExecutionEnabled)};systemMutations={reports.Sum(r=>r.dryRun.SystemMutations)}");
