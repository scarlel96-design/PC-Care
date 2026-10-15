param([Parameter(Mandatory=$true)][string]$FixtureRoot,[string]$ReceiptDirectory='artifacts/wo21-focused')
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$records=[Collections.Generic.List[object]]::new()
foreach($case in @(@{Root='relative-root';Expected='WO21_ROOT_ABSOLUTE_REQUIRED'},@{Root=$repo;Expected='WO21_ROOT_PROTECTED'},@{Root=(Join-Path $env:windir 'wo21-protected');Expected='WO21_ROOT_PROTECTED'})){
 $text=& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'wo21-qualification.ps1') -Mode ValidateOnly -FixtureRoot $case.Root 2>&1
 $code=$LASTEXITCODE
 $literal=($text|Out-String)
 if($code -eq 0 -or !$literal.Contains($case.Expected)){throw 'INVALID_ROOT_NOT_REJECTED'}
 $records.Add(@{Input=$case.Expected;ExitCode=$code;Output=$case.Expected;Result='Rejected'})
}
$receipt=Join-Path $repo ($ReceiptDirectory+'/build-receipt.json')
$original=[IO.File]::ReadAllBytes($receipt)
try{
 $copy=Get-Content -LiteralPath $receipt -Raw|ConvertFrom-Json
 $copy.InputDigest='STALE'
 $copy|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $receipt
 $text=& pwsh -NoProfile -File (Join-Path $PSScriptRoot 'wo21-qualification.ps1') -Mode Test -FixtureRoot $FixtureRoot -OutputDirectory $ReceiptDirectory -Filter 'FullyQualifiedName~WO21' 2>&1
 $code=$LASTEXITCODE
 if($code -eq 0 -or !($text|Out-String).Contains('WO21_STALE_BUILD_SOURCE')){throw 'STALE_RECEIPT_NOT_REJECTED'}
 $records.Add(@{Input='stale input digest';ExitCode=$code;Output='WO21_STALE_BUILD_SOURCE';Result='Rejected before testhost'})
}finally{[IO.File]::WriteAllBytes($receipt,$original)}
$records|ConvertTo-Json -Depth 5|Set-Content (Join-Path $repo ($ReceiptDirectory+'/environment-guards.json'))
'WO21_ENVIRONMENT_GUARDS=PASS'
