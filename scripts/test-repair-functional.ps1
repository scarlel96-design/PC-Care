param(
    [string]$Root = ""
)

$ErrorActionPreference = "Stop"
if (-not $Root) {
    $Root = Split-Path $PSScriptRoot -Parent
}

$helper = Join-Path $Root "smart_performance_doctor_repair_helper.exe"
$core = Join-Path $Root "smart_performance_doctor_core.exe"

Write-Host "== Repair functional smoke test ==" -ForegroundColor Cyan

if (-not (Test-Path $helper)) {
    throw "RepairHelper 없음: $helper"
}
if (-not (Test-Path $core)) {
    throw "Core 없음: $core"
}

Write-Host "[OK] RepairHelper exists" -ForegroundColor Green
Write-Host "[OK] Core exists" -ForegroundColor Green

# RepairHelper dry-run via named pipe (same protocol as app)
Add-Type -AssemblyName System.Core
$pipeName = "spd-test-" + [Guid]::NewGuid().ToString("N")
$nonce = [Guid]::NewGuid().ToString("N")
$request = @{
    id = "test-1"
    action = "driver_check_problem_devices"
    target = "online-image"
    dryRun = $true
    riskAccepted = $false
    nonce = $nonce
} | ConvertTo-Json -Compress

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
$responseLine = $reader.ReadLine()
$server.Dispose()
if (-not $proc.HasExited) { $proc.Kill() }

if ([string]::IsNullOrWhiteSpace($responseLine)) {
    throw "RepairHelper 응답 없음"
}

$response = $responseLine | ConvertFrom-Json
Write-Host "Status: $($response.status)" -ForegroundColor Gray
Write-Host "Message: $($response.message)" -ForegroundColor Gray

if ($response.status -notin @("dry-run", "ok", "planned")) {
    throw "RepairHelper dry-run 실패: $($response.status)"
}

Write-Host "[OK] RepairHelper dry-run functional test passed" -ForegroundColor Green