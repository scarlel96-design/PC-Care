param(
    [string]$Root = "",
    [int]$CoreTimeoutSec = 180
)

$ErrorActionPreference = "Stop"
if (-not $Root) {
    $Root = Split-Path $PSScriptRoot -Parent
}

function Write-Pass([string]$Message) { Write-Host "[PASS] $Message" -ForegroundColor Green }
function Write-Fail([string]$Message) { Write-Host "[FAIL] $Message" -ForegroundColor Red }
function Write-Info([string]$Message) { Write-Host "[INFO] $Message" -ForegroundColor Gray }

$failures = New-Object System.Collections.Generic.List[string]
$passes = 0

function Assert-True([bool]$Condition, [string]$Name) {
    if ($Condition) {
        $script:passes++
        Write-Pass $Name
    }
    else {
        $failures.Add($Name)
        Write-Fail $Name
    }
}

Write-Host "== Smart Performance Doctor Full Functional Audit ==" -ForegroundColor Cyan

# 1) 배포 아티팩트
$required = @(
    "SmartPerformanceDoctor.exe",
    "smart_performance_doctor_core.exe",
    "smart_performance_doctor_repair_helper.exe",
    "rules\inference_policies.json",
    "rules\system_rules.json",
    "Views\UnifiedCarePage.xbf"
)
foreach ($rel in $required) {
    $path = Join-Path $Root $rel
    Assert-True (Test-Path $path) "artifact exists: $rel"
}

# 2) dotnet tests
Push-Location $Root
try {
    dotnet test .\tests\SmartPerformanceDoctor.Tests\SmartPerformanceDoctor.Tests.csproj -c Release --nologo -v q | Out-Null
    Assert-True ($LASTEXITCODE -eq 0) "dotnet unit tests"
}
finally {
    Pop-Location
}

# 3) Core module runs
function Invoke-CoreModule([string]$Module) {
    $core = Join-Path $Root "smart_performance_doctor_core.exe"
    $req = @{ id = "audit-$Module"; method = "run_module"; params = @{ module = $Module; risk = "low" } } | ConvertTo-Json -Compress
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $core
    $psi.WorkingDirectory = $Root
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $p = [System.Diagnostics.Process]::Start($psi)
    $null = $p.StandardError.ReadToEndAsync()
    $stdoutTask = $p.StandardOutput.ReadToEndAsync()
    $p.StandardInput.WriteLine($req)
    $p.StandardInput.Close()

    $moduleTimeoutMs = if ($Module -in @("driver", "audio")) {
        [Math]::Max($CoreTimeoutSec, 180) * 1000
    } else {
        $CoreTimeoutSec * 1000
    }

    if (-not $stdoutTask.Wait($moduleTimeoutMs)) {
        if (-not $p.HasExited) { $p.Kill() }
        return $null
    }

    if (-not $p.HasExited) { $p.WaitForExit(5000) }

    $output = $stdoutTask.Result
    $responseMatch = [regex]::Match($output, '\{"frameType":"response".*$', [System.Text.RegularExpressions.RegexOptions]::Singleline)
    $responseLine = if ($responseMatch.Success) { $responseMatch.Value.Trim() } else { $null }

    if (-not $responseLine) { return $null }
    $statusMatches = [regex]::Matches($responseLine, '"status"\s*:\s*"(?<s>[^"]+)"')
    $lastStatus = if ($statusMatches.Count -gt 0) { $statusMatches[$statusMatches.Count - 1].Groups['s'].Value } else { "" }
    $scoreMatch = [regex]::Match($responseLine, '"score"\s*:\s*(?<n>\d+)')
    return [PSCustomObject]@{
        Status = $lastStatus
        Score = if ($scoreMatch.Success) { [int]$scoreMatch.Groups['n'].Value } else { 0 }
    }
}

foreach ($module in @("quick", "system", "driver", "audio", "selftest")) {
    $resp = Invoke-CoreModule $module
    $ok = $resp -and $resp.Status -eq "ok" -and $resp.Score -gt 0
    Assert-True $ok "core module run: $module (status=$($resp.Status), score=$($resp.Score))"
}

# 4) RepairHelper dry-run for all known actions
$actions = @(
    "driver_check_problem_devices",
    "pnputil_scan_devices",
    "audio_scan_devices",
    "audio_restart_stack",
    "dism_checkhealth"
)

Add-Type -AssemblyName System.Core
foreach ($action in $actions) {
    $pipeName = "spd-audit-" + [Guid]::NewGuid().ToString("N")
    $request = @{
        id = "audit-$action"
        action = $action
        target = "online-image"
        dryRun = $true
        riskAccepted = $false
        nonce = [Guid]::NewGuid().ToString("N")
    } | ConvertTo-Json -Compress

    $helper = Join-Path $Root "smart_performance_doctor_repair_helper.exe"
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $helper
    $psi.Arguments = "--pipe $pipeName"
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $server = New-Object System.IO.Pipes.NamedPipeServerStream(
        $pipeName,
        [System.IO.Pipes.PipeDirection]::InOut,
        1,
        [System.IO.Pipes.PipeTransmissionMode]::Byte,
        [System.IO.Pipes.PipeOptions]::Asynchronous)

    $proc = [System.Diagnostics.Process]::Start($psi)
    $server.WaitForConnection()
    $writer = New-Object System.IO.StreamWriter($server)
    $writer.WriteLine($request)
    $writer.Flush()
    $reader = New-Object System.IO.StreamReader($server)
    $line = $reader.ReadLine()
    $server.Dispose()
    if (-not $proc.HasExited) { $proc.Kill() }

    $ok = $false
    if ($line) {
        $payload = $line | ConvertFrom-Json
        $ok = $payload.status -in @("dry-run", "ok", "planned")
    }
    Assert-True $ok "repair helper dry-run: $action"
}

# 5) Knowledge DB file (runtime path writable)
$runtimeDbDir = Join-Path $env:LOCALAPPDATA "SmartPerformanceDoctor\data"
Assert-True (Test-Path $runtimeDbDir) "knowledge db directory exists"
$dbFile = Join-Path $runtimeDbDir "knowledge.db"
if (Test-Path $dbFile) {
    Assert-True ((Get-Item $dbFile).Length -gt 0) "knowledge db file readable"
}
else {
    Write-Info "knowledge db not created yet (first run will create it)"
    $passes++
    Write-Pass "knowledge db directory ready"
}

Write-Host ""
Write-Host "Audit summary: PASS=$passes FAIL=$($failures.Count)" -ForegroundColor Cyan
if ($failures.Count -gt 0) {
    Write-Host "Failed checks:" -ForegroundColor Red
    foreach ($f in $failures) { Write-Host " - $f" -ForegroundColor Red }
    exit 1
}

Write-Host "[OK] Full functional audit passed." -ForegroundColor Green
exit 0