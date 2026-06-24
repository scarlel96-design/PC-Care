<<<<<<< HEAD
# PC 케어 프로 v49

PC 통합 점검·복구·보안 관리 프로그램입니다.

## 폴더 구조

| 경로 | 용도 |
|------|------|
| `src/` | 소스 코드 |
| `scripts/` | 빌드·배포·설치 스크립트 |
| `content/` | 규칙·에셋 (소스) |
| `PCCare.exe` | **실행 프로그램** (빌드 후 프로젝트 루트에 배포) |
| `engine/`, `runtimes/` | 런타임 보조 파일 (빌드 후 생성) |
| `artifacts/` | 설치 파일·릴리즈 산출물 |
| `target/` | Rust 네이티브 엔진 빌드 |

빌드 후 실행 파일은 프로젝트 루트에 `PCCare.exe`로 배포됩니다.

## 빠른 시작

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\build.ps1 -SkipInstaller   # 빌드 + 실행 배포
.\PCCare.exe
```

전체 빌드 + 설치 파일:

```powershell
.\scripts\build.ps1
# 설치 파일: artifacts\installer\setup\SmartPerformanceDoctor_Setup_v49.0.0.exe
```

## 개발

```powershell
.\scripts\build.ps1 -SkipInstaller -SkipTests
dotnet test .\tests\SmartPerformanceDoctor.Tests\SmartPerformanceDoctor.Tests.csproj -c Release -p:Platform=x64
```

워크스페이스 정리 (루트에 남은 빌드 찌꺼기 제거):

```powershell
.\scripts\clean-workspace.ps1
```

## 데이터 저장 위치

- 지식 DB: `%LOCALAPPDATA%\SmartPerformanceDoctor\data\knowledge.db`
- Aegis Mirror: `%ProgramData%\AstraCare\AegisMirror\`
- 보고서: `%USERPROFILE%\Desktop\AstraCare\`
=======
>>>>>>> fd754d9974b6c0b5657a8047e30fc12ac0ebb6e8
