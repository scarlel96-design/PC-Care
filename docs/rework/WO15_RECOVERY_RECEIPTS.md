# WO-15 복구 영수증

작업 브랜치: `codex/pccare-vault-freshness-recovery-receipts`.
기준 HEAD: `b99d29f1591cb46ce56b299665648e486072d4b8`. 기준 트리: `56f048f3465316147a6bc52b1acb732b8c6825d1`.
검증 소스: `ab2c19f3027a5450a55042915af69220f7ec5ccc`. 소스 트리: `37233729e49b78d1b1dc4bbbe3d5416db72a15e0`.
환경: Windows 11 build 26200, .NET SDK 10.0.401, 논리 CPU 24개.
입력: 합성 LabFast fixture. 실제 사용자 금고와 설치본은 시험에 사용하지 않았다.
판정: **PARTIAL / Phase 1 NO-GO**.
명령과 입력·출력·종료 코드는 `evidence/wo15/`의 항목별 기록에서 확인한다.

## 실제 기록
영수증은 VaultId와 RecoveryOperationId를 기록한다.
복구 코드는 VMK 기반 HMAC 식별자로 기록한다. 원시 복구 코드를 저장하지 않는다.
인증 세대, 예약·확정·확인 시각, 세션 GUID와 앵커 결과를 함께 기록한다.
DPAPI 보호 기록의 영수증과 새 앵커 결합 값은 같은 state.dpapi 교체에 포함된다.

| 상태 | 영속성 |
|---|---|
| Unused | 예약 없음으로 표현 |
| Authenticated / SessionEstablished | 실제 호출 단계; 별도 영속 상태 없음 |
| Reserved | 소비 전에 영속 예약 |
| ConsumptionCommitted | 소비 단계; 독립 영수증 상태 기록 없음 |
| ReceiptPending | 소비 후 앵커+영수증 확정 |
| ReceiptAcknowledged | 호출자 확인 영속 기록 |
| Interrupted | 재조회된 Reserved의 가상 상태 |
| RecoveryReviewRequired | 정책 계약; 자동 해결 없음 |

동일 OperationId 조회는 새 코드 인증 없이 결과를 확인한다.
ReceiptAcknowledged의 재확인은 동일 기록을 반환한다.
예약된 코드나 OperationId의 재실행은 거부한다.
Interrupted를 자동 Unused로 바꾸지 않는다. 보수적 차단은 미소비 코드를 일시적으로 막을 수 있다.
영수증은 성공 응답이 사용자에게 정확히 한 번 보였음을 증명하지 않는다.
