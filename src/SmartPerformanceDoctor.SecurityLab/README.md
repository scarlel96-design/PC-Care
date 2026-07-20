# PCCare SecurityLab → 제품 50.3.0 병합

**상태:** 제품 탑재 완료 (50.3.0)  
**App 참조:** `SmartPerformanceDoctor.App` → `SmartPerformanceDoctor.SecurityLab`

## 제품 동작

| 기능 | 동작 |
|------|------|
| 신규 금고 | `spd-vault-v4-lab` (Argon2id Strong · 청크 AES-GCM) |
| 기존 v3 금고 | 레거시 경로 유지 |
| 보안 삭제 | ShredNext 경로 정책 + 파일 Lab 덮어쓰기 |
| 플래그 OFF | `PCCARE_SECURITYLAB=0` |

## 빌드

```powershell
dotnet test tests\SmartPerformanceDoctor.SecurityLab.Tests\SmartPerformanceDoctor.SecurityLab.Tests.csproj -c Release
dotnet build src\SmartPerformanceDoctor.App\SmartPerformanceDoctor.App.csproj -c Release -p:Platform=x64
dotnet run --project tools\SmartPerformanceDoctor.SecurityLab.Cli -c Release -- progress
```

진행률: 지시서 **100%** · Lab **100%** · 제품 **100%**.
