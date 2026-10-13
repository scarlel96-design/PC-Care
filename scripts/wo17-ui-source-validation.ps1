$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$care = [xml](Get-Content -Raw (Join-Path $repo 'src/SmartPerformanceDoctor.App/Views/SystemCareCenterPage.xaml'))
$ids = $care.SelectNodes('//@*[local-name()="AutomationProperties.AutomationId"]') | ForEach-Object Value
foreach ($id in @('StorageCareStatus','StorageCareStart','StorageCareStop','StorageCarePreview','StorageCareCandidates','StorageCarePlan')) {
    if (@($ids | Where-Object { $_ -eq $id }).Count -ne 1) { throw "Missing or duplicate automation ID: $id" }
}
$delete = [xml](Get-Content -Raw (Join-Path $repo 'src/SmartPerformanceDoctor.App/Views/SecureDeleteCenterPage.xaml'))
$code = Get-Content -Raw (Join-Path $repo 'src/SmartPerformanceDoctor.App/Views/SecureDeleteCenterPage.xaml.cs')
if ($code -notmatch 'ApplyDetailedAsync' -or $code -notmatch 'StatusLabel' -or $code -notmatch 'Targets.All') { throw 'Delete UI state contract missing' }
& (Join-Path $PSScriptRoot 'wo16-ui-source-validation.ps1')
if (-not $?) { throw 'Vault gate regression failed' }
Write-Output 'WO17_STORAGE_IDS=6;DELETE_RESULT_STATE=CONNECTED;FOLDER_EXECUTION=DISABLED;GUI=NOT_TESTED'
