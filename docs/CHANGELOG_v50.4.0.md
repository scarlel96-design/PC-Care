# PCCare 50.4.0 — 보안 금고 Lab v5 + AV3 게이트 승인 + Setup

## 하이라이트
- 보안 금고 출시 경로: **spd-vault-v5-lab** (SecurityLab)
- XChaCha 콘텐츠 · dual-header · journal · pack GC · recovery 재발급
- **AV3 ProductionWriter / Journal / Migration / ExternalReview 승인 (50.4.0 product GO)**
- 설치: `PCCare_Setup_v50.4.0.exe`
- 업데이트: `PCCare_Update_v50.4.0.spdup`
- Lab·설계 진행률 **100%**

## 제품 금고 경로
- UI/실사용 금고: SecurityLab v5 (변함 없음)
- AstraVault AV3: 프로덕션 게이트 승인 (연구/이관 경로 준비)

## 끄기
- `PCCARE_SECURITYLAB=0` 로 Lab UI 경로 비활성 가능

## 재빌드
```powershell
pwsh -File .\scripts\build-modular-setup.ps1 -Version 50.4.0
pwsh -File .\scripts\create-update-package.ps1 -FromVersion 50.3.0 -ToVersion 50.4.0
```
