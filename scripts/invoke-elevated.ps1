param(
    [Parameter(Mandatory = $true)]
    [string]$ScriptPath,
    [string[]]$ScriptArgs = @()
)

$ErrorActionPreference = "Stop"
$resolved = (Resolve-Path -LiteralPath $ScriptPath).Path
$argumentList = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $resolved) + $ScriptArgs

$proc = Start-Process -FilePath "pwsh.exe" `
    -ArgumentList $argumentList `
    -Verb RunAs `
    -PassThru `
    -Wait

if ($null -eq $proc) {
    throw "Elevated process did not start (UAC cancelled?): $resolved"
}

if ($proc.ExitCode -ne 0) {
    throw "Elevated script failed (exit $($proc.ExitCode)): $resolved"
}

Write-Host "[OK] Elevated script completed: $resolved" -ForegroundColor Green