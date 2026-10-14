# WO-18 — DELETE_TRANSACTION_MODEL

작업 브랜치: `codex/pccare-wo18-durable-delete-recovery`.
기준 HEAD: `5a407ea6399bd86d36a1ff6ef230109d61e659ed`. 기준 트리: `ba52e593ce599bca6ab162045b0bea31f7618e1c`.
최종 실행 검증 소스: `d3467ea91ad916ff405705939f0c62c65e70153f`. 소스 트리: `70902843929bfe50e16a2892ccb2f7b0932e8c82`.
초기 로컬 검증 소스: `92c3258e998b27b5a1b51570dbd4da7fc86d4e4f`.
최종 원격 HEAD와 CI 검증 소스는 `evidence/wo18/GITHUB_RESULT.txt`에서 확인하십시오.
최종 자동 시험 집계는 `evidence/wo18/FULL_TEST_RESULT.txt`에서 확인하십시오.
상태: PARTIAL. Phase 1 공식 출시: NO-GO.
실제 사용자 파일, 금고, 계정 및 Windows 설정을 변경하지 않았습니다.

## 구현
`SecureDeleteJournal`은 대상별 상태 전이를 검증합니다.
전역 상태와 대상별 상태는 별도로 기록합니다.
전역 Completed는 결과 문서의 봉인 완료입니다. 파일별 Completed와 구분합니다.

대상 전이:
Planned → PreflightVerified → TargetHandleBound → DestructiveWriteIntent → WriteInProgress → WriteFinished → VerificationFinished → DeleteRequested → DeleteDispositionAccepted → DeletionObserved → AuditCommitted → Completed.

변경 전 취소는 CancelledBeforeMutation으로 기록합니다.
파괴적 의도 이후 불확실한 상태는 RecoveryReviewRequired로 기록합니다.
잘못된 전이, 대상 인덱스, OperationId를 거부합니다.
의도 레코드의 Flush가 완료돼야 첫 쓰기를 시작합니다.
저널 쓰기 실패는 해당 저널의 후속 쓰기를 차단합니다.
다른 OperationId도 동일 파일 ID의 기존 예약을 재사용하지 못합니다.
파일 ID 예약은 보수적으로 보존합니다. 자동 해제 정책은 아직 없습니다.

## 제한
레코드는 각 패스의 바이트 진행량을 인증하지 않습니다.
WriteInProgress는 일부 쓰기가 가능하다는 의미입니다.
기록 실패 이후 이미 변경된 데이터를 원복하지 않습니다.
미완료 OperationId를 자동으로 재실행하지 않습니다.

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
