$ErrorActionPreference = "Stop"

Write-Host "== Build WinUI App ==" -ForegroundColor Cyan

dotnet restore .\src\SmartPerformanceDoctor.Contracts\SmartPerformanceDoctor.Contracts.csproj
dotnet restore .\src\SmartPerformanceDoctor.App\SmartPerformanceDoctor.App.csproj

dotnet build .\src\SmartPerformanceDoctor.Contracts\SmartPerformanceDoctor.Contracts.csproj -c Release
dotnet build .\src\SmartPerformanceDoctor.App\SmartPerformanceDoctor.App.csproj -c Release -p:Platform=x64

.\scripts\copy-native-engines.ps1

Write-Host "[OK] WinUI app build completed." -ForegroundColor Green
