param(
    [string]$Root = ""
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path $PSScriptRoot -Parent
if (-not $Root) { $Root = $ProjectRoot }

$excludePatterns = @(
    "*\tests\*\TestAssets\*",
    "*\artifacts\signing\*",
    "*\bin\*",
    "*\obj\*",
    "*\dist\*",
    "*\target\*",
    "*\scripts\*"
)

$hits = New-Object System.Collections.Generic.List[string]
Get-ChildItem $Root -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object {
        $path = $_.FullName
        $excluded = $false
        foreach ($pat in $excludePatterns) {
            if ($path -like $pat) { $excluded = $true; break }
        }
        -not $excluded
    } |
    ForEach-Object {
        try {
            $text = Get-Content $_.FullName -Raw -ErrorAction Stop
            if ($text -match "-----BEGIN (?:EC )?PRIVATE KEY-----[\r\n]+[A-Za-z0-9+/=\r\n]+-----END (?:EC )?PRIVATE KEY-----") {
                $hits.Add($_.FullName)
            }
        }
        catch {
            # binary or locked file
        }
    }

if ($hits.Count -gt 0) {
    Write-Host "[FAIL] Private key material detected in source tree:" -ForegroundColor Red
    $hits | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    exit 1
}

Write-Host "[OK] No private key material in scanned source tree." -ForegroundColor Green