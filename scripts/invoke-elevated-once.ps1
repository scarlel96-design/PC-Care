param(
    [Parameter(Mandatory = $true)]
    [string]$ScriptPath,
    [string[]]$ScriptArgs = @(),
    [int]$TimeoutSec = 1800
)

$ErrorActionPreference = "Stop"
$taskName = "PCCare_RcLock_Elevated_$(Get-Random)"
$logOut = Join-Path $env:TEMP "PCCare-elevated-out.log"
$logErr = Join-Path $env:TEMP "PCCare-elevated-err.log"
if (Test-Path $logOut) { Remove-Item $logOut -Force }
if (Test-Path $logErr) { Remove-Item $logErr -Force }

$argLine = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$ScriptPath`"") + $ScriptArgs
$argString = ($argLine | ForEach-Object { if ($_ -match '\s') { "`"$_`"" } else { $_ } }) -join ' '

$action = New-ScheduledTaskAction -Execute "pwsh.exe" -Argument $argString
$principal = New-ScheduledTaskPrincipal -UserId "$env:USERDOMAIN\$env:USERNAME" -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Seconds $TimeoutSec)
Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Settings $settings -Force | Out-Null

try {
    Start-ScheduledTask -TaskName $taskName
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        $info = Get-ScheduledTaskInfo -TaskName $taskName
        if ($info.LastTaskResult -ne 267011) { break }
        Start-Sleep -Seconds 2
    }
    $result = (Get-ScheduledTaskInfo -TaskName $taskName).LastTaskResult
    if (Test-Path $logOut) { Get-Content $logOut }
    if (Test-Path $logErr) { Get-Content $logErr -ErrorAction SilentlyContinue }
    exit $result
}
finally {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
}