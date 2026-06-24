param(
    [switch]$SkipIfNoCert
)

$ErrorActionPreference = "Stop"

Write-Host "== Sign release artifacts ==" -ForegroundColor Cyan
& "$PSScriptRoot\sign-consumer.ps1" @PSBoundParameters