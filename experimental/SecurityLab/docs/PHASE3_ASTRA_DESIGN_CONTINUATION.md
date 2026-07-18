# 3차 개편 — Astra 설계 연속 (패키지 보류)

**원칙:** 완성 전 **설치 패키지 미생성**. 코드·테스트·App 빌드만.

## P0 (본 라운드) — 완료 · 패키지 없음

| ID | 설계 | 상태 |
|----|------|------|
| P3-01 | §6 XChaCha 우선 | ✅ SPDCHK4 + HChaCha20 |
| P3-02 | §14 저널 복구 | ✅ unlock 시 incomplete → Aborted |
| P3-03 | §9 Step-up | ✅ >50 항목 export 확인 대화상자 |
| P3-04 | §10 보안 상태 | ✅ SecurityStateText UI |
| P3-05 | §7 Concealed 입문 | ✅ 소형 청크 4KiB 패딩 |

**테스트:** 15/15 · App 빌드 OK · **Setup 패키지 생성 안 함**

## 보류

- 설치 Setup/MSI 패키징 (완성 전 금지)
- AV3 ProductionWriter enable
- Sentinel AI 모델
- packfile / fixed-size full Concealed
