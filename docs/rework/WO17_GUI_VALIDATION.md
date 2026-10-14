# WO-17 — GUI 검증 상태

상태: NOT TESTED. Phase 1 출시 판정: NO-GO.

작업 브랜치: `codex/pccare-wo17-safe-storagecare-secure-delete`.
기준 HEAD: `229ede0c0488c2e878f212a4d6420bfb10c44a36`. 기준 트리: `3ab0390bf592ff7035511c0891084a4b6e639e63`.
검증 소스: `63febc9ca94d8da90e5bfb3696f85f4ccc0ec125`. 소스 트리: `4d14aecd85a0962cd8664ea4bf8a1ec6e2dfdaba`.
Windows 11 Pro 26200, .NET SDK 10.0.401, 논리 CPU 24개에서 실행했습니다.
최종 증거 커밋과 CI 결과는 `evidence/wo17/GITHUB_RESULT.txt`에서 확인하십시오.

GuiProbe는 Release 빌드 경고 0, 오류 0으로 빌드했습니다.
StorageCare 자동화 ID 6개와 삭제 결과 상태 연결을 정적 검사했습니다.
AV3 승인 게이트 6개가 false임을 확인했습니다.
정적 검사는 실제 GUI 조작이 아닙니다.
검증된 독립 사용자 SID, 폐기 가능한 VM과 하이퍼바이저 스냅샷이 제공되지 않았습니다.
앱을 실제 사용자 프로필에서 실행하는 시험은 수행하지 않았습니다.
다음 실행 절차를 사용하십시오.
1. 독립 Windows 테스트 계정과 복원 가능한 VM 스냅샷을 확인하십시오.
2. 기존 `scripts/wo16-gui-validation.ps1`의 SID 경계를 검증하십시오.
3. GuiProbe의 `--exercise-storage-page` 경로를 실행하십시오.
4. 생성한 합성 파일만 보안 삭제 화면에 추가하십시오.
5. 취소, 좁은 창, DPI, 키보드와 스크린리더 결과를 기록하십시오.
환경 확인 파일: `VM_ENVIRONMENT.txt`. 실제 GUI 결과: `GUI_TEST_RESULT.txt`.
