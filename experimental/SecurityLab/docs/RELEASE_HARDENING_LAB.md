# SecurityLab / 향후 제품 병합 시 릴리즈 하드닝

## 허용 (합법)

- Release 최적화, PDB 분리, 심볼 strip  
- Authenticode 코드 서명  
- 업데이트 패키지 해시·서명 검증  
- 설정/헤더 MAC·해시  
- 민감 문자열 최소화, 시크릿 하드코딩 금지  
- 변조 감지 시 금고 자동 잠금  
- 디버거 연결 시 **경고** (선택)

## 금지

- 루트킷, 프로세스 은폐, AV/EDR 우회  
- 무단 권한 상승, 몰래 상주  
- 보안 제품 회피용 난독화  
- “디버거 감지 시 데이터 파괴”

## 빌드 권고

```powershell
dotnet publish ... -c Release
# 이후 signtool / Azure Sign 등
```

## 상세 정책

난독화 허용/금지 범위는 `OBFUSCATION_AND_HARDENING_POLICY.md` 를 따른다.

## Lab CLI 점검

```powershell
dotnet run --project experimental\SecurityLab.Cli -c Release -- policy-selfcheck
dotnet run --project experimental\SecurityLab.Cli -c Release -- hardening
dotnet run --project experimental\SecurityLab.Cli -c Release -- av3-gate
dotnet run --project experimental\SecurityLab.Cli -c Release -- progress
```

`hardening` 은 런타임 프로브 + **LabReleaseHardeningChecklist** (ship-core 게이트) 를 출력한다.
PackageAllowed 는 완성 전 **false** (의도).

## Ship-core (Lab 출시 경로)

| ID | 항목 | 기대 |
|----|------|------|
| R1 | AV3 ProductionWriter OFF | true |
| R2 | AV3 enable 미승인 | true |
| R3 | LabAadBoundary pass | true |
| R4 | UI state labels full | true |
| R5–R8 | broker / session / recovery reissue / FI | true |
| R9 | 절대보안 마케팅 금지 | true |
| R10 | 설치 패키지 보류 | 정책 hold |

## 복구 재발급 (제품 UX)

- 비밀번호 변경 → 이전 복구 코드 **전부 무효** + 새 10개
- 복구 코드 재발급(비밀번호 확인) → 동일
- 복구 세션(`RecoveryAvailable`) 후 비밀번호 변경 → `Unlocked` 로 복귀
