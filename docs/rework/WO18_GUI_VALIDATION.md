# WO-18 — GUI_VALIDATION

작업 브랜치: `codex/pccare-wo18-durable-delete-recovery`.
기준 HEAD: `5a407ea6399bd86d36a1ff6ef230109d61e659ed`. 기준 트리: `ba52e593ce599bca6ab162045b0bea31f7618e1c`.
최종 실행 검증 소스: `d3467ea91ad916ff405705939f0c62c65e70153f`. 소스 트리: `70902843929bfe50e16a2892ccb2f7b0932e8c82`.
초기 로컬 검증 소스: `92c3258e998b27b5a1b51570dbd4da7fc86d4e4f`.
최종 원격 HEAD와 CI 검증 소스는 `evidence/wo18/GITHUB_RESULT.txt`에서 확인하십시오.
최종 자동 시험 집계는 `evidence/wo18/FULL_TEST_RESULT.txt`에서 확인하십시오.
상태: PARTIAL. Phase 1 공식 출시: NO-GO.
실제 사용자 파일, 금고, 계정 및 Windows 설정을 변경하지 않았습니다.

## 결과
실제 GUI: NOT TESTED.
확인 가능한 폐기 VM, 스냅샷과 독립 SID를 확보하지 못했습니다.
로컬 preflight에서 Hyper-V 조회 cmdlet은 발견되지 않았습니다.
입력 문자열을 스냅샷 증거로 사용하지 않습니다.
사용자 계정을 생성하거나 기존 프로필에서 위험한 시험을 실행하지 않았습니다.
GuiProbe 빌드와 정적 AutomationId 검사는 실제 UI 조작과 구분합니다.

## 실행 절차
1. 폐기 가능한 Windows VM을 준비하십시오.
2. 하이퍼바이저에서 스냅샷 ID를 확인하십시오.
3. 원래 사용자와 다른 표준 사용자 SID를 준비하십시오.
4. AppData, TEMP와 합성 작업 루트를 분리하십시오.
5. wo18-gui-preflight.ps1에 검증된 VM과 SID를 지정하십시오.
6. 기존 GuiProbe로 Dashboard, SystemCare, StorageCare, Delete, Vault 화면을 조작하십시오.
7. 취소, 미완료, 감사 실패와 폴더 차단 상태를 확인하십시오.
8. 키보드, 스크린리더, 고 DPI, 좁은 창과 축소 모션을 시험하십시오.
9. 조작 로그, 화면과 종료 코드를 별도로 저장하십시오.
동일 코드의 헤드리스 시험을 GUI PASS로 사용하지 마십시오.

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
