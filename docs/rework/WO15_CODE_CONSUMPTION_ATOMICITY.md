# WO-15 코드 소비와 성공 전달

작업 브랜치: `codex/pccare-vault-freshness-recovery-receipts`.
기준 HEAD: `b99d29f1591cb46ce56b299665648e486072d4b8`. 기준 트리: `56f048f3465316147a6bc52b1acb732b8c6825d1`.
검증 소스: `ab2c19f3027a5450a55042915af69220f7ec5ccc`. 소스 트리: `37233729e49b78d1b1dc4bbbe3d5416db72a15e0`.
환경: Windows 11 build 26200, .NET SDK 10.0.401, 논리 CPU 24개.
입력: 합성 LabFast fixture. 실제 사용자 금고와 설치본은 시험에 사용하지 않았다.
판정: **PARTIAL / Phase 1 NO-GO**.
명령과 입력·출력·종료 코드는 `evidence/wo15/`의 항목별 기록에서 확인한다.

## 실행 순서
세션 잠금을 확보한 후 복구 코드를 예약한다.
인증된 코드 소비 상태를 기록한다. 변경된 앵커와 ReceiptPending을 같은 보호 레코드에 확정한다.
호출자가 확인하면 ReceiptAcknowledged를 기록한다.
서로 다른 두 저장소의 변경을 하나의 원자적 연산으로 주장하지 않는다.
소비와 앵커 확정 사이의 중단은 AuthenticButStale와 Interrupted를 남긴다.
기존 비밀번호로 읽기 전용 파일 바이트는 확인됐다. 코드 결과의 자동 복구는 미완성이다.

## 별도 프로세스 종료 결과
| 종료 지점 | 앵커 상태 | 영수증 | 별도 리더 |
|---|---|---|---|
| TrustedBeforeAuthentication | FreshnessVerified | NoReservation | BYTES_VERIFIED |
| TrustedAfterAuthentication | FreshnessVerified | NoReservation | BYTES_VERIFIED |
| TrustedSessionEstablished | FreshnessVerified | NoReservation | BYTES_VERIFIED |
| TrustedBeforeConsumption | FreshnessVerified | Interrupted | BYTES_VERIFIED |
| TrustedAfterConsumption | AuthenticButStale | Interrupted | BYTES_VERIFIED |
| TrustedBeforeResponse | FreshnessVerified | ReceiptPending | BYTES_VERIFIED |
| TrustedResponseReady | FreshnessVerified | ReceiptPending | BYTES_VERIFIED |
| TrustedBeforeAcknowledgement | FreshnessVerified | ReceiptPending | BYTES_VERIFIED |
| TrustedAfterAcknowledgement | FreshnessVerified | ReceiptAcknowledged | BYTES_VERIFIED |
| AnchorBeforeReplace | FreshnessVerified | NoReservation | BYTES_VERIFIED |
| AnchorAfterReplace | FreshnessVerified | Interrupted | BYTES_VERIFIED |
| AnchorBeforeReplace:2 | AuthenticButStale | Interrupted | BYTES_VERIFIED |
| AnchorAfterReplace:2 | FreshnessVerified | ReceiptPending | BYTES_VERIFIED |
| AnchorBeforeReplace:3 | FreshnessVerified | ReceiptPending | BYTES_VERIFIED |
| AnchorAfterReplace:3 | FreshnessVerified | ReceiptAcknowledged | BYTES_VERIFIED |

15개 종료 시험에서 별도 리더 exit 0과 원본 바이트를 확인했다.
이 결과는 앵커·복구 코드 exactly-once 전체 PASS가 아니다.
물리 전원 차단과 VM 전원 종료는 NOT TESTED다.
