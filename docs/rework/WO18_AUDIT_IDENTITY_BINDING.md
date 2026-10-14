# WO-18 — AUDIT_IDENTITY_BINDING

작업 브랜치: `codex/pccare-wo18-durable-delete-recovery`.
기준 HEAD: `5a407ea6399bd86d36a1ff6ef230109d61e659ed`. 기준 트리: `ba52e593ce599bca6ab162045b0bea31f7618e1c`.
최종 실행 검증 소스: `d3467ea91ad916ff405705939f0c62c65e70153f`. 소스 트리: `70902843929bfe50e16a2892ccb2f7b0932e8c82`.
초기 로컬 검증 소스: `92c3258e998b27b5a1b51570dbd4da7fc86d4e4f`.
최종 원격 HEAD와 CI 검증 소스는 `evidence/wo18/GITHUB_RESULT.txt`에서 확인하십시오.
최종 자동 시험 집계는 `evidence/wo18/FULL_TEST_RESULT.txt`에서 확인하십시오.
상태: PARTIAL. Phase 1 공식 출시: NO-GO.
실제 사용자 파일, 금고, 계정 및 Windows 설정을 변경하지 않았습니다.

## 원인과 변경
WO-17 감사 지문은 최종 쓰기 핸들 확보 전에 계산됐습니다.
WO-18은 최종 배타적 핸들에서 전체 콘텐츠 해시와 파일 ID를 계산합니다.
볼륨 ID, 파일 ID, 길이 및 소유권을 같은 핸들의 증거에 결합합니다.
선택 지문과 최종 지문을 비교합니다.
파괴적 의도 전에 콘텐츠와 소유권을 다시 확인합니다.
감사 체인은 최종 핸들의 콘텐츠 지문과 파일 ID를 사용합니다.
AUDIT_BINDING_RESULT.json은 실제 1MiB 합성 파일의 결합 결과입니다.
AUDIT_BINDING_RESULT의 시간은 기록 검증 비용입니다. 전체 해시 비용과 구분합니다.
삭제 실행의 전체 시간과 작업 집합은 DELETE_COMPLETED_RESULT에서 확인합니다.
KDF 비용은 이 파일 삭제 측정에 포함되지 않습니다.

## 감사 실패
파일이 삭제돼도 감사 실패는 전체 성공으로 표시하지 않습니다.
삭제 관찰 수와 인증된 완료 수를 구분합니다.
누락된 키, 감사, 결과 문서는 수동 검토 상태를 반환합니다.
기록 실패로 삭제된 콘텐츠가 복원됐다고 주장하지 않습니다.
기존 writable memory mapping을 통한 동시 콘텐츠 변경은 NOT TESTED입니다.
같은 SID의 저장소 전체 조작은 사용자 계정 수준 인증의 한계입니다.

## 동일 해시 API 실측

WindowsFileBoundary.FileLease.Hash를 실제 합성 파일에서 반복 실행했습니다.
1MiB 중앙값은 2.2034ms입니다. 16MiB는 19.6378ms입니다.
64MiB 중앙값은 78.7344ms입니다.
64MiB 측정의 관리 할당은 약 1.45~1.58MB입니다.
프로세스 전체 최고 작업 집합은 36,499,456바이트입니다.
파일 생성 비용과 KDF는 해시 시간에서 제외했습니다.
작업 집합 최고값은 프로세스 전체 수명 값입니다.
반복 시험은 파일 캐시 영향을 포함합니다. 저사양 장비 결과로 일반화하지 않습니다.
입력, 실제 CPU 시간과 모든 반복값은 HASH_BUDGET_RESULT.json에 있습니다.

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
