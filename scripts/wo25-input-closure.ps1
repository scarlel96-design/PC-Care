function Open-Wo25InputClosure {
 param([string]$Root,[string]$Manifest,[string]$Signature,[string]$PublicKey,[string]$ExpectedKeyHash)
 $ErrorActionPreference='Stop'
 if(![IO.Path]::IsPathFullyQualified($Root)){throw 'DEPLOYMENT_ABSOLUTE_ROOT_REQUIRED'}
 $rootPath=[IO.Path]::GetFullPath($Root).TrimEnd('\')
 $raw=[IO.File]::ReadAllBytes($Manifest);$sig=[IO.File]::ReadAllBytes($Signature);$public=[IO.File]::ReadAllBytes($PublicKey)
 if($raw.Length -gt 2097152 -or $sig.Length -ne 64 -or $public.Length -gt 4096){throw 'DEPLOYMENT_INPUT_LIMIT'}
 if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($public)) -ne $ExpectedKeyHash){throw 'DEPLOYMENT_KEY_REJECTED'}
 $key=[Security.Cryptography.ECDsa]::Create()
 try {
  $consumed=0;$key.ImportSubjectPublicKeyInfo($public,[ref]$consumed)
  if($consumed -ne $public.Length -or $key.KeySize -ne 256 -or !$key.VerifyData($raw,$sig,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)){throw 'DEPLOYMENT_SIGNATURE_REJECTED'}
 } finally {$key.Dispose()}
 function Assert-JsonUnique($element){
  if($element.ValueKind -eq [Text.Json.JsonValueKind]::Object){
   $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
   foreach($property in $element.EnumerateObject()){if(!$names.Add($property.Name)){throw 'DEPLOYMENT_DUPLICATE_PROPERTY'};Assert-JsonUnique $property.Value}
  } elseif($element.ValueKind -eq [Text.Json.JsonValueKind]::Array){foreach($item in $element.EnumerateArray()){Assert-JsonUnique $item}}
 }
 $json=[Text.Json.JsonDocument]::Parse([Text.Encoding]::UTF8.GetString($raw))
 try {Assert-JsonUnique $json.RootElement} finally {$json.Dispose()}
 $document=[Text.Encoding]::UTF8.GetString($raw)|ConvertFrom-Json
 if($document.Format -ne 'pccare-test-deployment-v1' -or !$document.Version -or !$document.Publisher -or $document.Files.Count -lt 1 -or $document.Files.Count -gt 4096){throw 'DEPLOYMENT_MANIFEST_REJECTED'}
 $streams=[Collections.Generic.List[IDisposable]]::new()
 try {
  $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
  foreach($item in $document.Files){
   if(!$item.Path -or $item.Path.Contains('\') -or [IO.Path]::IsPathRooted($item.Path) -or ($item.Path.Split('/')|Where-Object {$_ -in '.','..','' -or $_.Contains(':')}) -or !$names.Add($item.Path)){throw 'DEPLOYMENT_PATH_REJECTED'}
   $path=[IO.Path]::GetFullPath((Join-Path $rootPath $item.Path))
   if(!$path.StartsWith($rootPath+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'DEPLOYMENT_ESCAPE'}
   for($ancestor=$path;$ancestor;$ancestor=Split-Path $ancestor -Parent){if((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'DEPLOYMENT_REPARSE'}}
   $stream=[IO.FileStream]::new($path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read);$streams.Add($stream)
   if($stream.Length -ne $item.Length -or [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)) -ne $item.Sha256){throw 'DEPLOYMENT_CONTENT_REJECTED'}
   $stream.Position=0;$mz=$stream.ReadByte() -eq 77 -and $stream.ReadByte() -eq 90
   if([bool]$item.IsPe -ne $mz -or ([IO.Path]::GetExtension($path) -in '.exe','.dll' -and !$mz)){throw 'DEPLOYMENT_PE_CLASSIFICATION_REJECTED'}
  }
  $actual=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
  $pending=[Collections.Generic.Stack[string]]::new();$pending.Push($rootPath);$visited=0
  while($pending.Count){
   $directory=$pending.Pop();if(++$visited -gt 4096){throw 'DEPLOYMENT_DIRECTORY_LIMIT'}
   foreach($item in Get-ChildItem -LiteralPath $directory -Force){
    if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'DEPLOYMENT_REPARSE'}
    if($item.PSIsContainer){$pending.Push($item.FullName)}else{[void]$actual.Add([IO.Path]::GetRelativePath($rootPath,$item.FullName).Replace('\','/'))}
   }
  }
  if(!$actual.SetEquals($names)){throw 'DEPLOYMENT_CLOSURE_REJECTED'}
  foreach($item in $document.Files){
   if(!$item.Path.EndsWith('.json',[StringComparison]::OrdinalIgnoreCase)){continue}
   $configPath=Join-Path $rootPath $item.Path
   if((Get-Item -LiteralPath $configPath).Length -gt 8388608){throw 'DEPLOYMENT_JSON_LIMIT'}
   $configJson=[Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($configPath))
   try {Assert-JsonUnique $configJson.RootElement} finally {$configJson.Dispose()}
   $runtimeConfig=Get-Content -LiteralPath $configPath -Raw|ConvertFrom-Json -AsHashtable
   if($item.Path.EndsWith('.runtimeconfig.json',[StringComparison]::OrdinalIgnoreCase) -and $runtimeConfig.ContainsKey('runtimeOptions') -and $runtimeConfig.runtimeOptions.ContainsKey('additionalProbingPaths') -and @($runtimeConfig.runtimeOptions.additionalProbingPaths).Count -gt 0){throw 'DEPLOYMENT_EXTERNAL_PROBING'}
   if(!$item.Path.EndsWith('.deps.json',[StringComparison]::OrdinalIgnoreCase)){continue}
   $config=Get-Content -LiteralPath $configPath -Raw|ConvertFrom-Json -AsHashtable
   if(!$config.ContainsKey('targets')){continue}
   foreach($target in $config.targets.Values){foreach($library in $target.Values){foreach($kind in @('runtime','native','runtimeTargets')){
    if(!$library.ContainsKey($kind)){continue}
    foreach($asset in $library[$kind].Keys){
     if($asset.EndsWith('/_._',[StringComparison]::Ordinal)){continue}
     $assetPath=$asset.Replace('\','/')
     if([IO.Path]::IsPathRooted($assetPath) -or ($assetPath.Split('/')|Where-Object {$_ -in '.','..','' -or $_.Contains(':')})){throw 'DEPLOYMENT_DEPENDENCY_PATH'}
     $resolved=if($kind -eq 'runtime'){[IO.Path]::GetFileName($assetPath)}else{$assetPath}
     $parent=[IO.Path]::GetDirectoryName($item.Path).Replace('\','/')
     if($parent){$resolved=$parent+'/'+$resolved}
     if(!($document.Files.Path|Where-Object {$_.Equals($resolved,[StringComparison]::OrdinalIgnoreCase)})){throw 'DEPLOYMENT_DEPENDENCY_MISSING'}
    }
   }}}
  }
  [PSCustomObject]@{Status='AUTHENTICATED_CONTENT_BOUND';Root=$rootPath;Manifest=$document;Leases=$streams;KeyHash=$ExpectedKeyHash;ActualPeSignatureChecked=$false}
 } catch {foreach($stream in $streams){$stream.Dispose()};throw}
}

function Assert-Wo26RuntimeEnvironment {
 foreach($item in Get-ChildItem Env:){
  if($item.Value -and $item.Name -match '^(DOTNET_STARTUP_HOOKS|DOTNET_ADDITIONAL_DEPS|DOTNET_SHARED_STORE|CORECLR_.*PROFIL.*|COR_.*PROFIL.*|COMPlus_.*)$'){throw 'DEPLOYMENT_RUNTIME_ENVIRONMENT_REVIEW'}
 }
}
