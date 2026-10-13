$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$pagePath = Join-Path $repo 'src/SmartPerformanceDoctor.App/Views/SecureVaultCenterPage.xaml'
$page = [xml](Get-Content -Raw -LiteralPath $pagePath)
$ids = $page.SelectNodes('//@*[local-name()="AutomationProperties.AutomationId"]') | ForEach-Object Value
foreach ($id in @('VaultStatus','VaultFormat','VaultCompatibilityNotice','VaultPasswordChange','VaultPasswordRestriction','VaultEntries')) {
    if (@($ids | Where-Object { $_ -eq $id }).Count -ne 1) { throw "Missing or duplicate automation ID: $id" }
}
foreach ($script in @('wo15-gui-validation.ps1','wo16-gui-validation.ps1')) {
    $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $script),[ref]$null,[ref]$errors)
    if ($errors.Count) { throw "PowerShell syntax errors: $script" }
}
$gates = Get-Content -Raw -LiteralPath (Join-Path $repo 'src/SmartPerformanceDoctor.AstraVault/Target/Av3PhaseGate.cs')
foreach ($gate in @('ProductionWriterEnabled','JournalWriterEnabled','MigrationEnabled','WriterEnableReady','ExternalReviewCompleted','ProductionEnableAuthorized')) {
    if ($gates -notmatch ("\b" + $gate + '\s*=\s*false\s*;')) { throw "Production gate changed: $gate" }
}
Write-Output 'UI_SOURCE_IDS=6;SCRIPT_SYNTAX=2;AV3_FALSE_GATES=6;GUI=NOT_TESTED'
