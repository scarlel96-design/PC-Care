# WO-18 — PROTECTED_STORAGE_POLICY

작업 브랜치: `codex/pccare-wo18-durable-delete-recovery`.
기준 HEAD: `5a407ea6399bd86d36a1ff6ef230109d61e659ed`. 기준 트리: `ba52e593ce599bca6ab162045b0bea31f7618e1c`.
최종 실행 검증 소스: `d3467ea91ad916ff405705939f0c62c65e70153f`. 소스 트리: `70902843929bfe50e16a2892ccb2f7b0932e8c82`.
초기 로컬 검증 소스: `92c3258e998b27b5a1b51570dbd4da7fc86d4e4f`.
최종 원격 HEAD와 CI 검증 소스는 `evidence/wo18/GITHUB_RESULT.txt`에서 확인하십시오.
최종 자동 시험 집계는 `evidence/wo18/FULL_TEST_RESULT.txt`에서 확인하십시오.
상태: PARTIAL. Phase 1 공식 출시: NO-GO.
실제 사용자 파일, 금고, 계정 및 Windows 설정을 변경하지 않았습니다.

## 보호 등록
기존 StorageTargetPolicy의 기본 보호를 유지합니다.
StorageProtectionRegistry는 사용자가 등록한 보호 루트를 추가합니다.
종류는 Vault, Recovery, Anchor, Audit, Backup, Keys, Updater, User입니다.
각 항목은 절대 경로, 루트 파일 ID와 종류를 저장합니다.
DPAPI CurrentUser로 등록 자료를 보호합니다.
등록은 배타적 잠금, 별도 임시 파일과 File.Replace를 사용합니다.
내용 손상과 초기화 이후 등록 파일 부재를 실패로 처리합니다.
등록 자료가 손상되면 기본 조회는 정리 후보를 보호합니다.
등록 루트의 신원이 변경돼도 경로 보호를 유지합니다.
최대 128개 보호 루트를 허용합니다.

## 정리 허용 목록
UserTemp 공급자는 공식 .NET GetTempPath 위치만 관찰합니다.
임시 경로도 사용자 자료를 포함할 수 있습니다.
이 공급자는 읽기 전용이며 실행 권한이 없습니다.
임의 공급자 ID와 다른 Temp 루트를 거부합니다.
생산 사용자 폴더 정리는 비활성화 상태입니다.
등록 파일과 표식을 함께 되돌리는 같은 SID의 재생 방어는 미완료입니다.
최신성 앵커로 등록 자료를 표현하지 않습니다.

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
