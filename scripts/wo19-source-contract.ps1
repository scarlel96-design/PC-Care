$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$app = [IO.File]::ReadAllText((Join-Path $repo 'src/SmartPerformanceDoctor.App/App.xaml.cs'))
$options = [IO.File]::ReadAllText((Join-Path $repo 'src/SmartPerformanceDoctor.App/Services/AppLaunchOptions.cs'))
$window = [IO.File]::ReadAllText((Join-Path $repo 'src/SmartPerformanceDoctor.App/UpdateRecoveryWindow.cs'))
$pending = [IO.File]::ReadAllText((Join-Path $repo 'src/SmartPerformanceDoctor.App/Services/Update/UpdatePendingApplier.cs'))
if (!$options.Contains('--readonly-update-status')) { throw 'Read-only entry point missing' }
if ($app.IndexOf('new UpdateRecoveryWindow') -gt $app.IndexOf('Shell = new MainWindow')) { throw 'Recovery gate runs after the normal shell' }
foreach ($id in @('UpdateRecoveryStatus','UpdateRecoveryRefresh','UpdateRecoveryRequest','UpdateRecoveryClose')) {
    if (!$window.Contains($id)) { throw "Missing automation identifier: $id" }
}
if ($pending.Contains('VerifyInstalledVersion(toVersion)')) { throw 'Legacy version-only completion remains enabled' }
'WO19_SOURCE_CONTRACT=PASS; LIVE_GUI=SEPARATE'
