# 2차 개편 — Astra Vault 설계 반영 (Phase 2)

**기준 설계:** 사용자 제공 Astra Vault / 보안 금고 설계서  
**제품 버전 목표:** 50.4.0  
**원칙:** AV3 production writer는 여전히 별도 게이트. 본 트랙은 **출시 경로(SecurityLab → 제품 금고)** 를 설계에 가깝게 강화.

## Phase 2 범위 (P0)

| ID | 설계 절 | 구현 |
|----|---------|------|
| P2-01 | §4 Locator | `vault.locator.json` public bounded |
| P2-02 | §5 Header 다중 사본 | generation + copy0/copy1 + 무결성 |
| P2-03 | §5 Recovery | 복구코드로 VMK unwrap (일회 슬롯) |
| P2-04 | §8 Read-only unlock | `UnlockReadOnly` |
| P2-05 | §11 Password change | v5 비밀번호 변경 (VMK 유지 rewrap) |
| P2-06 | §14 Journal | import 트랜잭션 Prepared→Committed |
| P2-07 | §2 Streaming | 대용량 ImportFile 스트리밍 청크 |
| P2-08 | §9 Policy | 대량 export Sentinel 결정적 게이트 |
| P2-09 | §12 Secure delete | 기존 ShredNext 유지·고지 강화 |
| P2-10 | 문서/진행률 | PHASE2 트래커 · 테스트 |

## 의도적 보류 (AV3 본체 게이트)

- XChaCha20 기본 스위트 전환 (코드 후보만 AstraVault)
- packs/ 고정 세그먼트 Concealed Profile
- ProductionWriter / crash kill full matrix 제품 활성화
- Sentinel AI 모델 연결 (결정적 Policy만)

## 포맷

- **신규:** `spd-vault-v5-lab` (Magic AVLT5)
- **기존 v4:** 계속 open 가능 (읽기/쓰기 호환 레이어)
