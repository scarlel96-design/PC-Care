# WO-18 — STORAGE_ACCOUNTING

작업 브랜치: `codex/pccare-wo18-durable-delete-recovery`.
기준 HEAD: `5a407ea6399bd86d36a1ff6ef230109d61e659ed`. 기준 트리: `ba52e593ce599bca6ab162045b0bea31f7618e1c`.
최종 실행 검증 소스: `d3467ea91ad916ff405705939f0c62c65e70153f`. 소스 트리: `70902843929bfe50e16a2892ccb2f7b0932e8c82`.
초기 로컬 검증 소스: `92c3258e998b27b5a1b51570dbd4da7fc86d4e4f`.
최종 원격 HEAD와 CI 검증 소스는 `evidence/wo18/GITHUB_RESULT.txt`에서 확인하십시오.
최종 자동 시험 집계는 `evidence/wo18/FULL_TEST_RESULT.txt`에서 확인하십시오.
상태: PARTIAL. Phase 1 공식 출시: NO-GO.
실제 사용자 파일, 금고, 계정 및 Windows 설정을 변경하지 않았습니다.

## 필드
LogicalBytes는 대상의 논리 크기입니다.
AllocatedBytes는 측정값이 없으면 null입니다.
QuarantinedBytes는 이동한 파일의 크기입니다.
RestorableBytes는 현재 복원 가능한 격리본의 크기입니다.
FinalDeletedBytes는 최종 삭제한 합성 콘텐츠의 크기입니다.
VerifiedFreedBytes는 작업에 귀속할 수 있는 공간 확보량입니다.
UnattributedVolumeDelta는 다른 작업의 영향을 포함할 수 있는 볼륨 변화입니다.
기존 필드와 호환되는 별칭을 시험 도구의 계약에 추가했습니다.
StorageInventory의 실제 AllocatedBytes 조회는 기존 API를 유지합니다.

## 실제 관찰
같은 볼륨 1MiB 격리의 VerifiedFreedBytes는 0입니다.
복원 가능 용량은 1MiB입니다. 최종 삭제 용량은 0입니다.
볼륨 여유 공간의 변화는 정리 이익에 귀속하지 않습니다.
QUARANTINE_RESULT.json에 원본 수치를 보존했습니다.
파일 용량을 Windows 속도 개선율로 변환하지 않습니다.

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

## 수정 후 실제 측정

Execute 이전 여유 공간은 43,557,683,200바이트입니다. Execute 이후 값은 43,557,679,104바이트입니다.
관찰 변화는 -4,096바이트입니다. 이 값은 파일 격리의 공간 확보량이 아닙니다.
VerifiedFreedBytes는 0입니다. UnattributedVolumeDelta는 -4,096바이트입니다.
격리 시간은 186.1837ms입니다. 복원 해시는 원본 해시와 일치했습니다.
두 번째 복원 조회는 Restored를 반환했습니다. 추가 파일 변경 수는 0입니다.

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
