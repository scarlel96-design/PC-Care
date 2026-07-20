$ErrorActionPreference = "Stop"

Write-Host "== Core Smoke Test ==" -ForegroundColor Cyan

$coreCandidates = @(
    ".\target\release\smart_performance_doctor_core.exe",
    ".\src\SmartPerformanceDoctor.App\bin\x64\Release\net10.0-windows10.0.26100.0\smart_performance_doctor_core.exe",
    ".\src\SmartPerformanceDoctor.App\bin\Release\net10.0-windows10.0.26100.0\smart_performance_doctor_core.exe"
)

$core = $coreCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $core) {
    throw "Core EXE를 찾지 못했습니다. 먼저 .\scripts\build-core.ps1을 실행하세요."
}

$request = @{
    jsonrpc = "2.0"
    id = "smoke-selftest"
    method = "run_module"
    params = @{
        module = "selftest"
        risk = "low"
    }
} | ConvertTo-Json -Compress

$utf8 = New-Object Text.UTF8Encoding($false)
$psi = New-Object Diagnostics.ProcessStartInfo
$psi.FileName = (Resolve-Path -LiteralPath $core).Path
$psi.UseShellExecute = $false
$psi.CreateNoWindow = $true
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$previousInputEncoding = [Console]::InputEncoding
[Console]::InputEncoding = $utf8
$process = New-Object Diagnostics.Process
$process.StartInfo = $psi
$null = $process.Start()
$process.StandardInput.WriteLine($request)
$process.StandardInput.Close()
$text = $process.StandardOutput.ReadToEnd()
$errorText = $process.StandardError.ReadToEnd()
$process.WaitForExit()
[Console]::InputEncoding = $previousInputEncoding
Write-Host $text
if ($process.ExitCode -ne 0) {
    throw "Core process failed (exit $($process.ExitCode)): $errorText"
}
if ($text -notmatch '"frameType":"response"') {
    throw "Core smoke failed: final response frame not found."
}
if ($text -notmatch '"htmlReportPath"') {
    throw "Core smoke failed: htmlReportPath not found."
}

Write-Host "[OK] Core smoke completed with report path." -ForegroundColor Green
