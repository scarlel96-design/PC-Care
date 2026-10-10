param(
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [Parameter(Mandatory)][string]$SnapshotId,
    [Parameter(Mandatory)][ValidateSet('prepare','capture','apply','recover','acl-write-probe')][string]$Action,
    [string]$InstallRoot,
    [string]$PackagePath,
    [string]$Outcome = 'NOT TESTED'
)
$ErrorActionPreference = 'Stop'
$evidence = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
if (!(Test-Path -LiteralPath (Join-Path $evidence '.wo05-disposable-vm'))) { throw 'BLOCKED: disposable VM marker missing.' }
if (!$SnapshotId.Trim()) { throw 'BLOCKED: record the VM snapshot ID.' }
$machine = Get-CimInstance Win32_ComputerSystem
if (($machine.Manufacturer + ' ' + $machine.Model) -notmatch 'VMware|VirtualBox|Virtual Machine|KVM|QEMU|Xen|Parallels') {
    throw 'BLOCKED: a disposable VM was not identified.'
}
$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
$elevated = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$repo = Split-Path $PSScriptRoot -Parent
$state = [ordered]@{ Utc=[DateTime]::UtcNow.ToString('o'); SnapshotId=$SnapshotId; Action=$Action;
    Outcome=$Outcome; Elevated=$elevated; Manufacturer=$machine.Manufacturer; Model=$machine.Model }
if ($Action -eq 'prepare') {
    $cert = New-SelfSignedCertificate -Subject 'CN=PC-Care WO05 VM TEST ONLY' -Type CodeSigningCert -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy NonExportable
    $publicCert = Join-Path $evidence 'wo05-test.cer'
    Export-Certificate -Cert $cert -FilePath $publicCert | Out-Null
    Import-Certificate -FilePath $publicCert -CertStoreLocation Cert:\CurrentUser\Root | Out-Null
    Import-Certificate -FilePath $publicCert -CertStoreLocation Cert:\CurrentUser\TrustedPublisher | Out-Null
    $kit = Join-Path $repo 'tools/SmartPerformanceDoctor.VMTestKit/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.VMTestKit.exe'
    & $kit vm-profile (Join-Path $evidence 'trust') $cert.Thumbprint
    if ($LASTEXITCODE -ne 0) { throw 'Test profile creation failed.' }
    $trust = Join-Path $evidence 'trust'
    $acl = Get-Acl -LiteralPath $trust
    $acl.SetAccessRuleProtection($true,$false)
    $acl.SetAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.WindowsIdentity]::GetCurrent().User,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
    Set-Acl -LiteralPath $trust -AclObject $acl
    $state.TestCertificateThumbprint=$cert.Thumbprint
} else {
    $install = (Resolve-Path -LiteralPath $InstallRoot).Path
    if ((Split-Path $install -Leaf) -ne 'PC-Care-WO05-Lab') { throw 'Use the dedicated PC-Care-WO05-Lab installation.' }
    if ($Action -in @('apply','recover')) {
        if ($elevated) { throw 'Launch UAC from the standard-user VM session.' }
        $helper = Join-Path $install 'engine/SmartPerformanceDoctor.UpdateHelper.exe'
        $arguments = if ($Action -eq 'apply') { @('--apply',('"'+(Resolve-Path -LiteralPath $PackagePath).Path+'"'),'--wait-pid','2147483647') } else { @('--recover') }
        try {
            $process = Start-Process -FilePath $helper -ArgumentList $arguments -Verb RunAs -WindowStyle Hidden -Wait -PassThru
            $state.ExitCode=$process.ExitCode
            $state.Outcome=if($process.ExitCode -eq 0){'PASS'}else{'FAIL'}
        } catch [ComponentModel.Win32Exception] {
            $state.ExitCode=$_.Exception.NativeErrorCode
            $state.Outcome=if($_.Exception.NativeErrorCode -eq 1223){'UAC-CANCELLED'}else{'FAIL'}
        }
    }
    $state.Files=@(Get-ChildItem -LiteralPath $install -File -Recurse | ForEach-Object {
        [pscustomobject]@{Path=$_.FullName.Substring($install.Length); Sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
    $protected = Join-Path $env:ProgramData 'PC-Care/UpdateTransactions'
    if (Test-Path -LiteralPath $protected) {
        $state.Acl=@(Get-ChildItem -LiteralPath $protected -Recurse -Force | Select-Object -First 500 | ForEach-Object {
            $acl=Get-Acl -LiteralPath $_.FullName
            [pscustomobject]@{Path=$_.FullName;Owner=$acl.Owner;Sddl=$acl.Sddl;Protected=$acl.AreAccessRulesProtected}
        })
        if ($Action -eq 'acl-write-probe') {
            if ($elevated) { throw 'Run the write probe as a standard user.' }
            $probe=Join-Path $protected ('wo05-probe-'+[Guid]::NewGuid().ToString('N'))
            try { [IO.File]::WriteAllText($probe,'synthetic'); $state.Outcome='FAIL: standard-user write permitted' }
            catch [UnauthorizedAccessException] { $state.Outcome='PASS: standard-user write denied' }
        }
    }
}
$state | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $evidence ($Action+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'.json')) -Encoding utf8
$state.Outcome
# Reboot and force termination require manual VM actions. This script never reboots the host.
