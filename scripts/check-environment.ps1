$ErrorActionPreference = "Stop"

Write-Host "== Smart Performance Doctor v43 Environment Check ==" -ForegroundColor Cyan

function Test-Command($Name, $Command) {
    $exe = $Command.Split(" ")[0]
    if (-not (Get-Command $exe -ErrorAction SilentlyContinue)) {
        Write-Host "[MISSING] $Name" -ForegroundColor Red
        return $false
    }

    Write-Host "[OK] $Name" -ForegroundColor Green
    try {
        Invoke-Expression $Command
    } catch {
        Write-Host "[WARN] $Name 버전 확인 실패: $_" -ForegroundColor Yellow
    }
    return $true
}

$ok = $true
$ok = (Test-Command "dotnet SDK" "dotnet --info") -and $ok
$ok = (Test-Command "cargo" "cargo --version") -and $ok
$ok = (Test-Command "rustc" "rustc --version") -and $ok
$ok = (Test-Command "PowerShell 7" "pwsh --version") -and $ok

try {
    $os = Get-CimInstance Win32_OperatingSystem
    Write-Host "[INFO] OS: $($os.Caption) / Build $($os.BuildNumber)" -ForegroundColor Gray
    if ([int]$os.BuildNumber -lt 26100) {
        Write-Host "[WARN] v43은 Windows 11 최신 빌드/SDK 10.0.26100.0 기준입니다." -ForegroundColor Yellow
    }
} catch {
    Write-Host "[WARN] OS 정보 수집 실패: $_" -ForegroundColor Yellow
}

if (-not $ok) {
    throw "필수 개발 도구가 누락되었습니다."
}

Write-Host "Windows App SDK / Visual Studio 구성은 Visual Studio Installer에서 확인하세요." -ForegroundColor Yellow
