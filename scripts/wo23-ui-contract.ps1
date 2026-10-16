param([string]$OutputDirectory='artifacts/wo23-ui')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$out=Join-Path $repo $OutputDirectory;New-Item -ItemType Directory -Force $out|Out-Null
& (Join-Path $PSScriptRoot 'wo22-ui-contract.ps1')
$view=Get-Content (Join-Path $repo 'src/SmartPerformanceDoctor.App/ViewModels/UpdateStatusViewModel.cs') -Raw
foreach($required in @('_progressGate.IsCurrent(epoch)','_progressGate.Invalidate()','CancelPreparation()','Progress=0')){if(!$view.Contains($required)){throw "MISSING_UI_GUARD:$required"}}
@{Status='PASS';Scope='UI source and service connection contract only';ActualGUI='NOT TESTED';Source=(& git -C $repo rev-parse HEAD).Trim();Command='pwsh -NoProfile -File scripts/wo23-ui-contract.ps1';Input='Existing update recovery UI and generation-aware progress callbacks';Output='WO23_UI_SOURCE_CONTRACT=PASS; ACTUAL_GUI=NOT_TESTED';ExitCode=0}|ConvertTo-Json|Set-Content (Join-Path $out 'ui.json')
$gate=Get-Content (Join-Path $repo 'src/SmartPerformanceDoctor.AstraVault/Target/Av3PhaseGate.cs') -Raw
$flags=@{}
foreach($name in @('ProductionWriterEnabled','JournalWriterEnabled','MigrationEnabled','WriterEnableReady','ExternalReviewCompleted','ProductionEnableAuthorized')){
 if($gate -notmatch ('bool\s+'+$name+'\s*=\s*false;')){throw "PRODUCTION_GATE_CHANGED:$name"};$flags[$name]=$false
}
$storage=Get-Content (Join-Path $repo 'src/SmartPerformanceDoctor.App/Services/SystemCare/StorageInventory.cs') -Raw
if(!$storage.Contains('public bool CanExecute => false;')){throw 'STORAGECARE_EXECUTION_CHANGED'}
@{Status='PASS';Source=(& git -C $repo rev-parse HEAD).Trim();Command='wo23-ui-contract.ps1 production source audit';Input='Actual production declarations';Flags=$flags;StorageCareCanExecute=$false;PublisherPolicyChanged=$false;ExitCode=0}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $out 'production-gates.json')
'WO23_UI_SOURCE_CONTRACT=PASS; ACTUAL_GUI=NOT_TESTED'
