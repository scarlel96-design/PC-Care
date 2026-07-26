param(
    [string]$AppDirectory = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $AppDirectory) {
    $AppDirectory = Join-Path $projectRoot "src\SmartPerformanceDoctor.App"
}

$appRoot = (Resolve-Path $AppDirectory).Path
$xamlFiles = Get-ChildItem $appRoot -Recurse -File -Filter *.xaml |
    Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' }

$defined = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$references = [System.Collections.Generic.Dictionary[string, System.Collections.Generic.List[string]]]::new([StringComparer]::OrdinalIgnoreCase)

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    foreach ($match in [regex]::Matches($content, 'x:Key\s*=\s*"([^"]+)"')) {
        [void]$defined.Add($match.Groups[1].Value)
    }

    foreach ($match in [regex]::Matches($content, '\{StaticResource\s+([^\}\s]+)')) {
        $key = $match.Groups[1].Value
        if (-not $references.ContainsKey($key)) {
            $references[$key] = [System.Collections.Generic.List[string]]::new()
        }
        $references[$key].Add($file.FullName.Substring($appRoot.Length).TrimStart('\'))
    }
}

# WinUI built-in styles are not declared in the app's own merged dictionaries.
$platformKeys = @(
    'DefaultTextBlockStyle', 'DefaultButtonStyle', 'DefaultTextBoxStyle',
    'DefaultComboBoxStyle', 'DefaultCheckBoxStyle', 'DefaultListViewStyle',
    'DefaultListViewItemStyle', 'DefaultScrollViewerStyle', 'DefaultUserControlStyle'
)

$missing = @($references.Keys | Where-Object {
    -not $defined.Contains($_) -and $_ -notin $platformKeys
} | Sort-Object)

if ($missing.Count -gt 0) {
    $details = $missing | ForEach-Object { "$_ ($($references[$_] -join ', '))" }
    throw "Unresolved StaticResource keys: $($details -join '; ')"
}

Write-Host "[OK] XAML StaticResource audit: $($references.Count) references, $($defined.Count) app resource keys." -ForegroundColor Green