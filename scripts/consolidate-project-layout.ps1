$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $ProjectRoot

function Move-IfExists {
    param([string]$Source, [string]$Destination)
    if (-not (Test-Path $Source)) { return }
    $destParent = Split-Path $Destination -Parent
    if (-not (Test-Path $destParent)) {
        New-Item -ItemType Directory -Path $destParent -Force | Out-Null
    }
    if (Test-Path $Destination) {
        Get-ChildItem $Source -Force | ForEach-Object {
            $target = Join-Path $Destination $_.Name
            if ($_.PSIsContainer) {
                if (-not (Test-Path $target)) {
                    Move-Item $_.FullName $target -Force
                }
            }
            elseif (-not (Test-Path $target)) {
                Move-Item $_.FullName $target -Force
            }
        }
        if (-not (Get-ChildItem $Source -Force -ErrorAction SilentlyContinue)) {
            Remove-Item $Source -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
    else {
        Move-Item $Source $Destination -Force
    }
    Write-Host "[OK] $Source -> $Destination" -ForegroundColor Green
}

Write-Host "== Consolidate project folders ==" -ForegroundColor Cyan

Move-IfExists (Join-Path $ProjectRoot "installer") (Join-Path $ProjectRoot "artifacts\installer\templates")
Move-IfExists (Join-Path $ProjectRoot "portable") (Join-Path $ProjectRoot "artifacts\portable")
Move-IfExists (Join-Path $ProjectRoot "archive") (Join-Path $ProjectRoot "artifacts\archive")
Move-IfExists (Join-Path $ProjectRoot "docs") (Join-Path $ProjectRoot "artifacts\docs")

if (Test-Path (Join-Path $ProjectRoot "app")) {
    Remove-Item (Join-Path $ProjectRoot "app") -Recurse -Force
    Write-Host "[OK] Removed legacy app/" -ForegroundColor Green
}

Write-Host "[DONE] Project layout consolidated." -ForegroundColor Cyan