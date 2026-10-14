# WO-18 — CANCELLATION_SEMANTICS

작업 브랜치: `codex/pccare-wo18-durable-delete-recovery`.
기준 HEAD: `5a407ea6399bd86d36a1ff6ef230109d61e659ed`. 기준 트리: `ba52e593ce599bca6ab162045b0bea31f7618e1c`.
최종 실행 검증 소스: `d3467ea91ad916ff405705939f0c62c65e70153f`. 소스 트리: `70902843929bfe50e16a2892ccb2f7b0932e8c82`.
초기 로컬 검증 소스: `92c3258e998b27b5a1b51570dbd4da7fc86d4e4f`.
최종 원격 HEAD와 CI 검증 소스는 `evidence/wo18/GITHUB_RESULT.txt`에서 확인하십시오.
최종 자동 시험 집계는 `evidence/wo18/FULL_TEST_RESULT.txt`에서 확인하십시오.
상태: PARTIAL. Phase 1 공식 출시: NO-GO.
실제 사용자 파일, 금고, 계정 및 Windows 설정을 변경하지 않았습니다.

## 결과 계약
WasCancellationRequested는 취소 요청의 존재를 나타냅니다.
AnyMutationStarted는 파괴적 변경 시작을 나타냅니다.
CompletedTargets는 감사까지 확정된 대상 수입니다.
IncompleteTargets는 승인한 대상 중 확정되지 않은 수입니다.
PartialWritePossible은 변경이 시작됐지만 완료되지 않은 항목에만 적용합니다.
정상 완료 항목은 PartialWritePossible=false입니다.
RequiresManualReview는 불확실한 변경 또는 기록 실패를 나타냅니다.
AuditVerified는 감사 체인의 검증 결과입니다.

## UI
결과 모델은 삭제 관찰 수와 감사 완료 수를 구분합니다.
UI는 감사 완료 수와 미완료 수를 표시합니다.
취소와 일부 변경을 동시에 표시합니다.
최종 검증 완료에서만 진행률 100%를 보고합니다.
전체 완료 이외의 결과에서는 대상 선택을 보존합니다.
페이지 진입 시 읽기 전용 미완료 작업 검사를 실행합니다.
주요 작업은 UI 스레드 밖에서 실행합니다.
실제 GUI 조작과 접근성은 NOT TESTED입니다.

## 최종 검증 증거

CI 검증 소스는 `d3467ea91ad916ff405705939f0c62c65e70153f`입니다. 소스 트리는 `70902843929bfe50e16a2892ccb2f7b0932e8c82`입니다.
[Windows CI 38537248047](https://github.com/scarlel96-design/PC-Care/actions/runs/38537248047)는 SUCCESS입니다.
앱 전체는 1,214 PASS / 0 FAIL / 111 SKIP입니다. 신규 WO-18은 36 PASS입니다.
업데이트 128개와 성능 233개는 앱 전체 집계에 포함합니다. SecurityLab은 별도 36 PASS입니다.
Restore와 Release의 경고 및 오류는 각각 0입니다.
삭제의 별도 프로세스 중단 17건은 읽기 전용 조정과 다른 파일 보존을 확인했습니다.
기존 합성 격리 중단 6건은 원본 SHA-256과 복원 SHA-256이 일치했습니다.
CI 스모크와 정적 UI 시험은 실제 GUI 검증이 아닙니다. 실제 GUI는 NOT TESTED입니다.
독립 SID, 저사양 장비, 물리 전원 차단과 실제 저장장치 Purge는 미검증입니다.
전체 판정은 PARTIAL입니다. Phase 1 공식 출시 판정은 NO-GO입니다.
