# Phase 6 — Pack GC + fuzz-lite (패키지 보류)

## 구현

| ID | 내용 |
|----|------|
| P6-01 | `LabPackStore.Compact` — live 재패킹, tombstone 제거, 구 pack shred |
| P6-02 | `LabVaultService.CompactPacks` + Sentinel maintenance 게이트 |
| P6-03 | UI 「Pack 정리(GC)」 + 확인 대화상자 |
| P6-04 | fuzz-lite: 8회 랜덤 비트플립 AEAD 실패 |
| P6-05 | compact 통합 테스트 |

## 비활성 유지

- AV3 ProductionWriter OFF
- Setup/MSI 패키지 미생성
