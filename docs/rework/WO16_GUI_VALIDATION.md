# WO16 GUI VALIDATION

작업 브랜치: `codex/pccare-wo16-standard-mode-stability`.
기준 HEAD: `60fc9a6c812e5458d5367020b732ca0c199ea72e`. 기준 트리: `57d21a8fd06231145897e09bfdc09f9c45764e01`.
검증 소스: `1ca80b98f63ec953c952647fcf28f8032ac986a7`. 검증 소스 트리: `61dc70b16d6d359d562cc26f6fbf65c2e3ef2803`.
판정: **PARTIAL / Phase 1 NO-GO**.
명령·입력·실제 출력·종료 코드는 `evidence/wo16/`에 기록했다.
최종 증거 커밋과 원격 HEAD는 최종 제출 기록에서 확인한다.

## 실제 연결된 변경
기존 WinUI 디자인 토큰과 목록 연결을 유지했다.
Lab 형식의 비밀번호 변경 버튼을 비활성화하고 제한 사유를 표시한다.
핸들러도 Lab 형식 호출을 차단한다.
복구 코드 안내에서 위험한 비밀번호 변경 권고를 제거했다.
읽기 전용 추출과 원본 보존 안내를 표시한다.
VaultPasswordChange와 VaultPasswordRestriction AutomationId를 추가했다.
GuiProbe는 형식·호환성 안내·비밀번호 버튼 상태를 읽을 수 있다.

## 검증 결과
GuiProbe 빌드는 경고 0·오류 0이다.
6개 AutomationId, PowerShell 스크립트 2개 문법, AV3 false 게이트 6개를 검사했다.
독립 SID가 없는 실행 스크립트는 exit 4로 차단했다. 앱과 시험 디렉터리를 생성하지 않았다.
GUI 실행 결과는 NOT TESTED다. 소스 ID 검사와 XAML 빌드는 GUI 시험이 아니다.
고 DPI·스크린리더·키보드·좁은 창·축소 모션·대량 목록 반응성은 NOT TESTED다.

## 실행 절차
1. 준비한 폐기 가능한 VM 스냅샷을 하이퍼바이저에서 확인한다.
2. 독립 표준 사용자 계정으로 로그인한다.
3. AppData·TEMP·금고 경로와 ACL을 확인한다.
4. wo16-gui-validation.ps1에 실제 독립 SID와 확인한 SnapshotId를 전달한다.
5. 앱과 GuiProbe 경로를 전달하고 Launch를 명시한다.
6. 합성 파일만 사용하여 금고 화면을 조작한다.
7. UI Automation 결과와 실제 조작 결과를 별도로 기록한다.
스크립트의 SnapshotId 입력은 하이퍼바이저 확인을 대체하지 않는다.
v6 영수증 조정은 제품 UI에 연결하지 않았다. 시험 기능을 동작 버튼처럼 표시하지 않는다.
