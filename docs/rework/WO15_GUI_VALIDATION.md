# WO-15 실제 GUI 검증

작업 브랜치: `codex/pccare-vault-freshness-recovery-receipts`.
기준 HEAD: `b99d29f1591cb46ce56b299665648e486072d4b8`. 기준 트리: `56f048f3465316147a6bc52b1acb732b8c6825d1`.
검증 소스: `ab2c19f3027a5450a55042915af69220f7ec5ccc`. 소스 트리: `37233729e49b78d1b1dc4bbbe3d5416db72a15e0`.
환경: Windows 11 build 26200, .NET SDK 10.0.401, 논리 CPU 24개.
입력: 합성 LabFast fixture. 실제 사용자 금고와 설치본은 시험에 사용하지 않았다.
판정: **PARTIAL / Phase 1 NO-GO**.
명령과 입력·출력·종료 코드는 `evidence/wo15/`의 항목별 기록에서 확인한다.

## 환경 결과
실제 GUI: NOT TESTED. 독립 사용자 SID와 폐기 가능한 VM이 제공되지 않았다.
현재 계정은 실제 사용자 프로필이다. 금고 GUI 시험에 사용하지 않았다.
새 scripts/wo15-gui-validation.ps1은 전용 SID·VM 확인·SnapshotId를 요구한다.
동일 SID 입력은 디렉터리 생성 전에 exit 4로 차단됐다.
SnapshotId는 입력 증거다. 스크립트는 실제 hypervisor snapshot 존재를 검증하지 않는다.
독립 계정의 AppData를 확인하고 금고와 TEMP/TMP를 시험 경로로 분리한다.
로그·캐시 디렉터리는 준비 자산이다. 앱 전체 로그·캐시 리디렉션 구현은 아니다.
GuiProbe는 열린 금고 화면의 UI Automation 트리를 검사한다. 전체 기능 상호작용은 수동 검증이 필요하다.

## 실행 절차
1. Create a disposable Windows VM and a recoverable snapshot.
2. Create a dedicated test account with a different SID.
3. Build the app and GuiProbe from the verified source.
4. Run the script inside the dedicated interactive account.
5. Pass the original SID, test SID, and snapshot evidence.
6. Enable Launch only after the prerequisites pass.
7. Open the vault page and use synthetic files only.
8. Record each interaction and restore the VM snapshot afterward.

```powershell
powershell -NoProfile -File scripts/wo15-gui-validation.ps1 `
  -ExpectedTestSid TEST_SID -OriginalUserSid ORIGINAL_SID `
  -SnapshotId SNAPSHOT_ID -DisposableVmConfirmed `
  -AppPath VERIFIED_APP_PATH -ProbePath VERIFIED_GUI_PROBE_PATH -Launch
```

생성·잠금·해제·추출·복구 코드·경고·고 DPI·키보드·스크린리더·축소 모션·좁은 창은 모두 NOT TESTED다.
환경 준비 스크립트 제공을 GUI PASS로 계산하지 않았다.
