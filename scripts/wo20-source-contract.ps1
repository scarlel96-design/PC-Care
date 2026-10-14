$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$app=[IO.File]::ReadAllText((Join-Path $repo 'src/SmartPerformanceDoctor.App/App.xaml.cs'))
$installer=[IO.File]::ReadAllText((Join-Path $repo 'src/SmartPerformanceDoctor.App/Services/Update/UpdateInstallerService.cs'))
$window=[IO.File]::ReadAllText((Join-Path $repo 'src/SmartPerformanceDoctor.App/UpdateRecoveryWindow.cs'))
$bridge=[IO.File]::ReadAllText((Join-Path $repo 'tools/SmartPerformanceDoctor.UpdateBridge/Program.cs'))
if($app.IndexOf('_startupRecovery=') -gt $app.IndexOf('EnableProductHost(')){throw 'InspectionAfterProductServices'}
if(!$installer.Contains('BootstrapDispatch.Launch(')){throw 'BootstrapUiDispatchMissing'}
if(!$bridge.Contains('ExecuteLegacyRecovery(')){throw 'LegacyAdminExecutionMissing'}
foreach($id in @('LegacyRecoveryReview','LegacyRecoveryExecute','UpdateRecoveryStatus')){if(!$window.Contains($id)){throw 'AutomationIdMissing'}}
'WO20_UI_CONTRACT=PASS; LIVE_GUI=NOT_TESTED'
