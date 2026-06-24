param(
    [string]$RuntimeDir = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot "RuntimeLayout.ps1")
if (-not $RuntimeDir) {
    $RuntimeDir = Get-RuntimeRoot -ProjectRoot $ProjectRoot
}

if (-not (Test-Path $RuntimeDir)) {
    throw "Runtime directory not found: $RuntimeDir"
}

$keepRidPatterns = @("win", "win-x64", "win10-x64", "win11-x64")
$runtimesRoot = Join-Path $RuntimeDir "runtimes"
if (-not (Test-Path $runtimesRoot)) {
    Write-Host "[SKIP] No runtimes folder: $runtimesRoot" -ForegroundColor Yellow
    return
}

$removed = 0
Get-ChildItem $runtimesRoot -Directory | ForEach-Object {
    $name = $_.Name
    if ($keepRidPatterns -contains $name) {
        return
    }

    Remove-Item $_.FullName -Recurse -Force
    $removed++
    Write-Host "[TRIM] Removed RID: $name" -ForegroundColor DarkYellow
}

# browser-wasm and other stray native folders at root
foreach ($pattern in @("browser-wasm", "android-*", "ios*", "linux-*", "maccatalyst-*", "osx-*")) {
    Get-ChildItem $RuntimeDir -Directory -Filter $pattern -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item $_.FullName -Recurse -Force
        $removed++
        Write-Host "[TRIM] Removed stray: $($_.Name)" -ForegroundColor DarkYellow
    }
}

Write-Host "[OK] Runtime RID trim complete ($removed removed) -> $RuntimeDir" -ForegroundColor Green