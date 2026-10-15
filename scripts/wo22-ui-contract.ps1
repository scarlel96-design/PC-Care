$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$window=Get-Content (Join-Path $repo 'src/SmartPerformanceDoctor.App/UpdateRecoveryWindow.cs') -Raw
$coordinator=Get-Content (Join-Path $repo 'src/SmartPerformanceDoctor.App/Services/Update/UpdateRecoveryCoordinator.cs') -Raw
$page=Get-Content (Join-Path $repo 'src/SmartPerformanceDoctor.App/Views/UpdateStatusPage.xaml') -Raw
$view=Get-Content (Join-Path $repo 'src/SmartPerformanceDoctor.App/ViewModels/UpdateStatusViewModel.cs') -Raw
foreach($value in @('LegacyApprovalQuery','LegacyApprovalRequestId','LegacyRecoveryReview','LegacyRecoveryExecute')){if(!$window.Contains($value)){throw "Missing recovery UI contract: $value"}}
if(!$coordinator.Contains('InspectLegacyApproval(') -or !$page.Contains('UpdatePreparationCancel') -or !$view.Contains('CancelPreparation()')){throw 'WO22_UI_SERVICE_DISCONNECTED'}
'WO22_UI_SOURCE_CONTRACT=PASS; ACTUAL_GUI=NOT_TESTED'
