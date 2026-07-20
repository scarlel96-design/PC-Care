# Phase 9 — S급 연속 (상태 라벨 · kill suite · AV3 체크리스트)

## 구현
- `LabSecurityStateLabels` — 전 enum + NotCreated KR 라벨 단일 소스
- App `SecureVaultLabBackend` → 라벨 헬퍼 공유
- `LabFaultMatrix` 확장: 헤더 전부 손상 / 오브젝트 cipher / marker digest + kill suite
  - mid-import ObjectsReady
  - Prepared-only
  - MetadataReady (저널 이력에서 obj id 복구)
- `LabVaultJournal.ListIncompleteDetailed` — TX 이력 ObjectsReady object id 추적
- `Av3LabEnableChecklist` — enable 게이트 열거 (writer 여전히 OFF)
- CLI `av3-gate` 에 체크리스트 출력

## AV3
- `ProductionWriterEnabled` = **false**
- EnableAuthorized = **false** (서명 대기)

## Phase 9b
- `LabAadBoundary` — wrong gen/vaultid · empty stream · truncated · stream AAD fail
- `LabSessionPolicy.ApplyProductAutoLockMinutes` + `FormatCountdown`
- App: 1초 세션 카운트다운 UI · 자동 잠금 분 ↔ Lab idle 동기화

## Phase 9c
- `LabRecoverySlots.Snapshot` + UI 잔여/소진/부족 문구
- 복구 잠금 해제 → `RecoveryAvailable` + 비밀번호 변경 권고
- `LabStreamImportMatrix` — ≥1MiB stream roundtrip · max reject · mid-stream kill
- App SecurityLine에 복구 슬롯 상태 표시

## Phase 9d
- `ReissueRecoveryCodes` — 비밀번호 증명 후 슬롯 전량 재발급
- `ChangePassword` — 이전 코드 무효 명시 + `RecoveryAvailable`→`Unlocked`
- App UI: **복구 코드 재발급** 버튼 + 안내 다이얼로그
- `LabReleaseHardeningChecklist` + CLI `hardening` 연동 (ShipCoreReady, PackageAllowed=false)

## Phase 9e
- `LabContainerProbe` — locator/headers/activation/packs/recovery 비비밀 점검
- `LabWriteGate` — RO/AV3 경계 fail-closed (EnsureWriteUnlocked)
- `LabToAv3MigrationGate` — dry-run 허용 · **execute 항상 거부**
- CLI: `container-probe`, `migrate-av3-gate`, `av3-gate`에 Phase H 포함
- 판정: 패키지·AV3 미완이면 종합≥85여도 **CONDITIONAL GO** 유지

## Phase 9f
- `LabSelfCheckSuite` — AAD/labels/release/AV3/write/session/probe 통합
- CLI `self-check [--vault]`
- 무결성 검사: **잠금 상태**에서도 컨테이너 프로브
- SecurityLine에 컨테이너 n/m 상태 표시

## Phase 9g
- activation 마커 소실 시 v5 **self-heal** (write unlock) + RecoveryAvailable
- header digest multi-copy soft-check on unlock
- `LabShipReadiness` / CLI `ship-ready` — LabCore vs Installer vs AV3 final
- V3 migration notes 현행화 (execute re-import ready)

## Phase 9h
- primary header torn → write unlock **header self-heal** (dual rewrite + re-commit)
- `LabVaultHealth` + CLI `vault-health`
- `LabRateLimiter.GetSnapshot` · SecurityLine/health 연동
- App PolicyLine에 건강 요약

## Phase 9i
- export: `.spdlab.tmp` atomic write + post-write SHA256 verify
- `LabOrphanScanner` + unlock/manual purge of loose orphans
- `LabRemainingGaps` / CLI `remaining-gaps` (package·AV3 holds)

## Phase 9j — 완성 · 패키지 · 100%
- 사용자 완성 선언 → `LabReleaseState` package+design complete
- Setup: `artifacts/installer/setup/PCCare_Setup_v50.4.0.exe` (layout-only host; WiX MSI optional)
- 진행률: 종합/출시/S급 **100%** · 판정 **GO** (AV3 writer 여전히 OFF)

## 패키지
- **생성됨:** PCCare_Setup_v50.4.0.exe
