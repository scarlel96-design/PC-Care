[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$TrxPath,

    [int]$ExpectedPassed = 3,

    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

# Deliberately does not load PCCare assemblies. This is a separate-process TRX
# and file-hash verifier for locally executed operation tests, not an external
# or third-party verification decision.
if (-not (Test-Path -LiteralPath $TrxPath -PathType Leaf)) { throw "TRX file is missing: $TrxPath" }
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Split-Path -Parent $TrxPath) 'local-operation-trx-verification.json'
}

[xml]$trx = Get-Content -LiteralPath $TrxPath -Raw
$results = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
$passed = @($results | Where-Object { $_.outcome -eq 'Passed' })
$failed = @($results | Where-Object { $_.outcome -ne 'Passed' })
$hash = Get-FileHash -LiteralPath $TrxPath -Algorithm SHA256

$payload = [ordered]@{
    schema = 'pc-care.local-operation-trx-verifier.v1'
    verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    verifier = 'PowerShell XML reader and Get-FileHash; PCCare assemblies not loaded'
    trxPath = (Resolve-Path -LiteralPath $TrxPath).Path
    trxSha256 = $hash.Hash.ToLowerInvariant()
    expectedPassed = $ExpectedPassed
    passed = $passed.Count
    failedOrOther = $failed.Count
    results = @($results | ForEach-Object { [ordered]@{ name = $_.testName; outcome = $_.outcome; duration = $_.duration } })
    assessment = 'local separate-process verification passed only when result count and outcomes match; this is not third-party independent verification'
}

$payload | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Host "TRX verification: $OutputPath"
if ($passed.Count -ne $ExpectedPassed -or $failed.Count -ne 0) { exit 1 }
