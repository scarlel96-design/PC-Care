[CmdletBinding()]
param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot "..\..\artifacts\security-readiness"),
    [string[]]$EvidencePath = @(),
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'

# This gate intentionally does not send a delete/sanitize command.  It packages
# reproducible software evidence and hashes externally produced evidence without
# treating either as proof of physical erasure or independent review.
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runRoot = Join-Path $OutputRoot $stamp
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null

$testProject = Join-Path $repoRoot 'tests\SmartPerformanceDoctor.Tests\SmartPerformanceDoctor.Tests.csproj'
$trxPath = Join-Path $runRoot 'security-readiness.trx'
$filter = @(
    'FullyQualifiedName~SecureDeleteHardeningTests',
    'FullyQualifiedName~WinPeStoragePurgeExecutorTests',
    'FullyQualifiedName~StoragePurgeExecutionEvidenceTests',
    'FullyQualifiedName~StoragePurgeAttestationBundleTests',
    'FullyQualifiedName~StoragePurgePreExecutionGateTests',
    'FullyQualifiedName~StoragePurgeReadOnlyInspectionTests',
    'FullyQualifiedName~NvmeIdentifyReadOnlyEvidenceTests',
    'FullyQualifiedName~NvmeCommandEffectsReadOnlyEvidenceTests',
    'FullyQualifiedName~NvmeSanitizeStatusReadOnlyEvidenceTests',
    'FullyQualifiedName~LabVaultCryptoHardeningTests',
    'FullyQualifiedName~LabVaultHeaderAuthenticationTests'
) -join '|'

$testResult = [ordered]@{ executed = $false; exitCode = $null; command = $null; log = 'security-readiness-test.log' }
if (-not $SkipTests) {
    $testResult.executed = $true
    $testResult.command = "dotnet test `"$testProject`" -c Release --no-restore -m:1 -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:SelfContained=true --filter `"$filter`""
    & dotnet test $testProject -c Release --no-restore -m:1 -p:Platform=x64 -p:RuntimeIdentifier=win-x64 -p:SelfContained=true --logger "trx;LogFileName=$trxPath" --filter $filter *>&1 |
        Tee-Object -FilePath (Join-Path $runRoot $testResult.log)
    $testResult.exitCode = $LASTEXITCODE
}

$evidence = foreach ($path in $EvidencePath) {
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        $item = Get-Item -LiteralPath $path
        $hash = Get-FileHash -LiteralPath $path -Algorithm SHA256
        [ordered]@{ path = $item.FullName; bytes = $item.Length; sha256 = $hash.Hash.ToLowerInvariant(); captured = $true }
    }
    else {
        [ordered]@{ path = $path; captured = $false; reason = '파일을 찾을 수 없습니다.' }
    }
}

$softwarePassed = -not $SkipTests -and $testResult.exitCode -eq 0
$manifest = [ordered]@{
    schema = 'pc-care.security-readiness-gate.v1'
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    repository = $repoRoot
    host = [ordered]@{
        os = [Environment]::OSVersion.VersionString
        machine = $env:COMPUTERNAME
        powershell = $PSVersionTable.PSVersion.ToString()
        dotnet = (& dotnet --version)
    }
    automation = [ordered]@{
        test = $testResult
        gateStatus = if ($SkipTests) { 'software-tests-not-executed' } elseif ($softwarePassed) { 'software-evidence-collected' } else { 'software-tests-failed' }
    }
    suppliedEvidence = @($evidence)
    axisAssessment = [ordered]@{
        implementation = if ($softwarePassed) { 'requires final code review scoring' } elseif ($SkipTests) { 'not scored: automated tests were skipped' } else { 'not scored: software test failure' }
        automation = if ($softwarePassed) { 'bounded automated evidence available' } elseif ($SkipTests) { 'not scored: automated tests were skipped' } else { 'not scored: test failure' }
        actualOperation = 'not scored: this non-destructive collector contains no real-media execution result'
        independentVerification = 'not scored: supplied files are hashed but not independently approved by this collector'
    }
    limitations = @(
        'No sanitize, purge, overwrite, or delete command was dispatched by this script.',
        'A passing software test suite is not evidence of real-media sanitization.',
        'A SHA-256 hash records file identity; it does not establish authorship or independence.'
    )
}

$manifestPath = Join-Path $runRoot 'security-readiness-manifest.json'
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8
Write-Host "Security readiness evidence: $manifestPath"
if (-not $SkipTests -and -not $softwarePassed) { exit 1 }
