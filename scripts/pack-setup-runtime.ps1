param(
    [string]$SetupDir,
    [string]$OutputZip
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $SetupDir)) { throw "Setup dir not found: $SetupDir" }

$staging = Join-Path ([IO.Path]::GetTempPath()) ("spd-runtime-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $staging -Force | Out-Null
try {
    $include = @("*.exe", "*.dll", "*.json")
    foreach ($pattern in $include) {
        Get-ChildItem $SetupDir -Filter $pattern -File | ForEach-Object {
            Copy-Item $_.FullName (Join-Path $staging $_.Name) -Force
        }
    }
    if (Test-Path $OutputZip) { Remove-Item $OutputZip -Force }
    Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $OutputZip -CompressionLevel Optimal
    Write-Host "[OK] Setup runtime zip: $OutputZip" -ForegroundColor Green
}
finally {
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
}