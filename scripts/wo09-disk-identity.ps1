param([string]$OutputDirectory='artifacts/wo09/disk-identity')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $root | Out-Null
$private=Join-Path ([IO.Path]::GetTempPath()) ('wo09-storage-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $private | Out-Null
$acl=Get-Acl -LiteralPath $private
$acl.SetAccessRuleProtection($true,$false)
$sid=[Security.Principal.WindowsIdentity]::GetCurrent().User
$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'FullControl','ContainerInherit,ObjectInherit','None','Allow'))
Set-Acl -LiteralPath $private -AclObject $acl
try {
 $physical=@(Get-PhysicalDisk -ErrorAction Stop)
 $storage=@(Get-Disk -ErrorAction Stop | ForEach-Object {
   $disk=$_
   $match=@($physical | Where-Object UniqueId -eq $disk.UniqueId)
   @{Number=[int]$disk.Number;UniqueId=[string]$disk.UniqueId;BusType=[string]$disk.BusType;
     MediaType=if($match.Count -eq 1){[string]$match[0].MediaType}else{'Unknown'};
     PartitionCount=@(Get-Partition -DiskNumber $disk.Number -ErrorAction Stop).Count}
 })
 $inputPath=Join-Path $private 'storage.json'
 ConvertTo-Json -InputObject $storage -Depth 4 | Set-Content $inputPath
 $keyPath=Join-Path $private 'identity.key'
 $key=New-Object byte[] 32
 $rng=[Security.Cryptography.RandomNumberGenerator]::Create()
 $rng.GetBytes($key);$rng.Dispose()
 [IO.File]::WriteAllBytes($keyPath,$key)
 $probe=[IO.Path]::GetFullPath('tools/SmartPerformanceDoctor.DiagnosticsProbe/bin/x64/Release/net10.0-windows10.0.26100.0/SmartPerformanceDoctor.DiagnosticsProbe.exe')
 foreach($trial in 1..2) {
   & $probe --disk-identities $inputPath $keyPath (Join-Path $root "disk-$trial.json")
   if($LASTEXITCODE -ne 0){throw "DiskIdentityProbeFailed $LASTEXITCODE"}
 }
 Write-Output 'READ_ONLY_DISK_IDENTITY_COMPLETED trials=2 rawStorageIdsPublished=False'
} catch {
 @{status='BLOCKED';errorType=$_.Exception.GetType().Name;physicalMapping='Unknown'} | ConvertTo-Json | Set-Content (Join-Path $root 'disk-error.json')
 Write-Output 'DISK_IDENTITY_BLOCKED storage-query-unavailable'
} finally {
 foreach($file in @('storage.json','identity.key')) {
  $target=[IO.Path]::GetFullPath((Join-Path $private $file))
  if(-not $target.StartsWith($private+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Private path escaped'}
  if(Test-Path -LiteralPath $target){Remove-Item -LiteralPath $target}
 }
}
