# WO-18 — QUARANTINE_TRANSACTION

작업 브랜치: `codex/pccare-wo18-durable-delete-recovery`.
기준 HEAD: `5a407ea6399bd86d36a1ff6ef230109d61e659ed`. 기준 트리: `ba52e593ce599bca6ab162045b0bea31f7618e1c`.
최종 실행 검증 소스: `d3467ea91ad916ff405705939f0c62c65e70153f`. 소스 트리: `70902843929bfe50e16a2892ccb2f7b0932e8c82`.
초기 로컬 검증 소스: `92c3258e998b27b5a1b51570dbd4da7fc86d4e4f`.
최종 원격 HEAD와 CI 검증 소스는 `evidence/wo18/GITHUB_RESULT.txt`에서 확인하십시오.
최종 자동 시험 집계는 `evidence/wo18/FULL_TEST_RESULT.txt`에서 확인하십시오.
상태: PARTIAL. Phase 1 공식 출시: NO-GO.
실제 사용자 파일, 금고, 계정 및 Windows 설정을 변경하지 않았습니다.

## 재사용 범위
WO-10 SandboxRecovery와 WindowsFileBoundary를 재사용했습니다.
schema3은 명시적으로 선택한 합성 .tmp/.cache 파일명을 지원합니다.
생성된 전용 루트와 인증된 저널에 대상 이름을 결합합니다.
원본 크기, SHA-256, 파일 ID 및 루트 ID를 검증합니다.
동의에는 ActionId, RootId와 만료 시각을 결합합니다.
격리와 복원은 실제 동일 볼륨 이름 변경을 수행합니다.
충돌 시 기존 파일과 격리본을 함께 보존합니다.
1MiB 전체 파일의 원본·복원 SHA-256을 비교했습니다.
한글 이름의 합성 왕복도 자동 시험에 포함합니다.

## 한계
schema3은 생성한 결정적 시험 콘텐츠만 허용합니다.
임의 사용자 파일과 범용 다중 파일 트랜잭션은 아직 승인되지 않았습니다.
StorageInventory의 생산 CanExecute=false를 유지합니다.
시험 도구를 제품의 실행 계층에 연결하지 않았습니다.
기존 6개 강제 종료 복구 시험은 CI에서 다시 실행합니다.

## 반복 복원 추가 검증

선택 이름의 두 번째 복원 조회는 구형 fixture.dat를 찾고 실패했습니다.
수정 전 실제 합성 시험은 일반 이름과 한글 이름에서 2 FAIL을 기록했습니다.
인증된 저널의 Action.Target을 다시 조회하도록 수정했습니다.
소유권과 반복 복원을 포함한 16개 StorageProductSafety 시험이 통과했습니다.
DeleteProbe의 공간 기준값도 실제 Execute 이전에 수집하도록 수정했습니다.
이전 공간 JSON의 AvailableBefore는 격리 이후 관측값입니다.
이전 값을 조치 전후 실측으로 사용하지 않습니다.
최종 검증 소스는 d3467ea91ad916ff405705939f0c62c65e70153f입니다.
이전 CI 38536117030은 이 수정의 검증을 위해 취소했습니다.
취소된 실행을 전체 회귀 PASS로 분류하지 않습니다.

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
