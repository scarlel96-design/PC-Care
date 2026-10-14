# WO-18 — DIRECTORY_BOUNDARY

작업 브랜치: `codex/pccare-wo18-durable-delete-recovery`.
기준 HEAD: `5a407ea6399bd86d36a1ff6ef230109d61e659ed`. 기준 트리: `ba52e593ce599bca6ab162045b0bea31f7618e1c`.
최종 실행 검증 소스: `d3467ea91ad916ff405705939f0c62c65e70153f`. 소스 트리: `70902843929bfe50e16a2892ccb2f7b0932e8c82`.
초기 로컬 검증 소스: `92c3258e998b27b5a1b51570dbd4da7fc86d4e4f`.
최종 원격 HEAD와 CI 검증 소스는 `evidence/wo18/GITHUB_RESULT.txt`에서 확인하십시오.
최종 자동 시험 집계는 `evidence/wo18/FULL_TEST_RESULT.txt`에서 확인하십시오.
상태: PARTIAL. Phase 1 공식 출시: NO-GO.
실제 사용자 파일, 금고, 계정 및 Windows 설정을 변경하지 않았습니다.

## 생산 정책
폴더 전체 삭제는 계속 차단합니다.
SecureDeleteDirectoryReview는 읽기 전용이며 실행 권한을 부여하지 않습니다.
디렉터리 핸들, 파일 ID, 콘텐츠 지문과 현재 소유자를 비교합니다.
새 항목, 변경된 파일, 잠긴 파일, 재분석 지점과 보호 항목을 거부합니다.
기존 WindowsFileBoundary의 하드링크 검사를 재사용합니다.
검사 예산은 512항목, 깊이 4, 5초 경계 검사입니다.
파일별 해시는 64MiB, 총 해시는 256MiB로 제한합니다.
시간 예산은 개별 네이티브 호출을 강제 중단하는 보장은 아닙니다.

## 관찰된 제한
실제 Windows Directory.Move는 디렉터리 핸들 유지 중에도 성공했습니다.
시험은 후속 ValidateUnchanged가 변경된 신원을 거부하는지 확인합니다.
핸들이 모든 이름 변경을 막는다고 주장하지 않습니다.
전체 ACL 변경, ADS, 삭제 중 트리 변경과 물리 전원 차단의 실행 검증은 미완료입니다.
모든 경계가 검증되기 전 생산 폴더 삭제를 승인하지 않습니다.

## CI 시험 소유권 수정

첫 CI 38520807911은 신규 33 PASS / 3 FAIL을 기록했습니다.
폴더 양성 시험이 현재 SID 소유 조건을 충족하지 못했습니다.
관리자 Runner의 기본 소유권 차이를 정상 데이터로 가정한 시험 준비가 원인입니다.
현재 SID로 합성 파일 소유권을 지정하고 실제 값을 다시 확인했습니다.
제품 소유권 검사와 실행 게이트는 변경하지 않았습니다.
수정한 로컬 StorageProductSafety 시험은 16 PASS / 0 FAIL / 0 SKIP입니다.
첫 소유권 수정 후보는 d303cec1af6912d40a3f49369994248fa47daac4입니다.
제품 소스는 이전 로컬 검증 소스 92c3258과 동일합니다.
차이는 새 시험 fixture 준비와 CI 순서입니다.

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
