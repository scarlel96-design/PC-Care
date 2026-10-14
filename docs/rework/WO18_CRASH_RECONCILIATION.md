# WO-18 — CRASH_RECONCILIATION

작업 브랜치: `codex/pccare-wo18-durable-delete-recovery`.
기준 HEAD: `5a407ea6399bd86d36a1ff6ef230109d61e659ed`. 기준 트리: `ba52e593ce599bca6ab162045b0bea31f7618e1c`.
최종 실행 검증 소스: `d3467ea91ad916ff405705939f0c62c65e70153f`. 소스 트리: `70902843929bfe50e16a2892ccb2f7b0932e8c82`.
초기 로컬 검증 소스: `92c3258e998b27b5a1b51570dbd4da7fc86d4e4f`.
최종 원격 HEAD와 CI 검증 소스는 `evidence/wo18/GITHUB_RESULT.txt`에서 확인하십시오.
최종 자동 시험 집계는 `evidence/wo18/FULL_TEST_RESULT.txt`에서 확인하십시오.
상태: PARTIAL. Phase 1 공식 출시: NO-GO.
실제 사용자 파일, 금고, 계정 및 Windows 설정을 변경하지 않았습니다.

## 읽기 전용 조정
별도 DeleteProbe 프로세스가 저널, 감사 체인 및 현재 객체를 조회합니다.
삭제나 덮어쓰기를 재개하지 않습니다.
같은 경로에 다른 파일이 있으면 TargetIdentityChanged를 반환하고 보존합니다.
경로 부재만으로 삭제 완료를 인정하지 않습니다.
마지막 전역 봉인, 대상 Completed, 삭제 요청 수락, 감사 인증과 결과 봉인을 함께 검사합니다.
완전한 근거가 있을 때만 DeletionConfirmedByAvailableEvidence를 반환합니다.
손상 또는 미완료 증거는 RecoveryReviewRequired를 반환합니다.
원본 내용이 변경된 상태를 원본 복원으로 표시하지 않습니다.

## 별도 프로세스 시험
17개 체크포인트에서 작업 프로세스를 종료했습니다.
각 재조회는 별도 프로세스에서 실행했습니다.
다른 합성 파일의 SHA-256을 함께 확인했습니다.
명령과 종료 코드는 DELETE_CRASH_MATRIX.json에 있습니다.
초기 로컬 행렬은 최종 소스 이전의 구현을 사용했습니다.
최종 CI 행렬과 소스 SHA를 별도로 보존합니다.
프로세스 강제 종료는 실제 전원 차단 시험이 아닙니다.

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
