param([ValidateSet('baseline','modified','rollback')][string]$Stage)
$ErrorActionPreference='Stop'
$root=Join-Path ([IO.Path]::GetFullPath('artifacts/wo09/fixture')) $Stage
New-Item -ItemType Directory -Force $root | Out-Null
$evidence=[IO.Path]::GetFullPath('docs/rework/evidence/wo09')
$copy=Join-Path ([IO.Path]::GetFullPath('artifacts/wo09/fixture')) 'rollback-copy'
if($Stage -eq 'rollback') {
 $source=Get-Content -LiteralPath (Join-Path $copy 'ReferenceComparison.cs') -Raw
} else {
 $name=if($Stage -eq 'baseline'){'ORIGINAL_FILE.cs'}else{'MODIFIED_FILE.cs'}
 $source=Get-Content -LiteralPath (Join-Path $evidence $name) -Raw
}
$code=$source.Substring(0,$source.IndexOf('public static class DiagnosticPresentation'))
Set-Content (Join-Path $root 'ReferenceComparison.cs') $code -Encoding utf8
@'
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>
'@ | Set-Content (Join-Path $root 'fixture.csproj') -Encoding utf8
@'
using SmartPerformanceDoctor.Diagnostics;
var result=ReferenceComparison.Evaluate([new Finding("CpuSaturation","InsufficientEvidence")],[new("CpuSaturation",true)],true,true);
Console.WriteLine($"TP={result.TruePositive};FP={result.FalsePositive};FN={result.FalseNegative};TN={result.TrueNegative};Unknown={result.Unknown};Insufficient={result.InsufficientEvidence};Mismatch={result.ReferenceMismatch};Precision={result.Precision?.ToString()??"null"};Recall={result.Recall?.ToString()??"null"}");
namespace SmartPerformanceDoctor.Diagnostics { public sealed record Finding(string Category,string Severity); }
'@ | Set-Content (Join-Path $root 'Program.cs') -Encoding utf8
dotnet run --project (Join-Path $root 'fixture.csproj') -c Release
exit $LASTEXITCODE

