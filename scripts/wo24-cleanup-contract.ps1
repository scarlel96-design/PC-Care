param([Parameter(Mandatory=$true)][string]$SourceFile)
$ErrorActionPreference='Stop'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile([IO.Path]::GetFullPath($SourceFile),[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'SOURCE_PARSE_FAILED'}
$finally=$ast.FindAll({param($node)$node -is [Management.Automation.Language.TryStatementAst] -and $null -ne $node.Finally},$true)[-1].Finally.Extent.Text
$code=$finally.Substring(1,$finally.Length-2)
$script:deleteKeyObserved=$false
function Test-Path {param($LiteralPath) return $true}
function Remove-Item {param($LiteralPath,[switch]$DeleteKey,$ErrorAction)
 if($DeleteKey){$script:deleteKeyObserved=$true}
 if($ErrorAction -eq 'Stop'){throw 'InjectedProviderFailure'}
}
$cert=[pscustomobject]@{Thumbprint='SYNTHETIC-NOT-A-CERTIFICATE'}
$surfaced=$false
try{& ([scriptblock]::Create($code))}catch{$surfaced=$true}
@{Command='wo24-cleanup-contract.ps1 -SourceFile <SOURCE_COPY>';Input='Mock certificate provider rejects both removals';CleanupFailureSurfaced=$surfaced;DeleteKeyRequested=$script:deleteKeyObserved;ActualCertificateStoreTouched=$false;ExitCode=0}|ConvertTo-Json
