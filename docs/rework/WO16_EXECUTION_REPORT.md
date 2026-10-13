# WO16 EXECUTION REPORT

작업 브랜치: `codex/pccare-wo16-standard-mode-stability`.
기준 HEAD: `60fc9a6c812e5458d5367020b732ca0c199ea72e`. 기준 트리: `57d21a8fd06231145897e09bfdc09f9c45764e01`.
검증 소스: `1ca80b98f63ec953c952647fcf28f8032ac986a7`. 검증 소스 트리: `61dc70b16d6d359d562cc26f6fbf65c2e3ef2803`.
판정: **PARTIAL / Phase 1 NO-GO**.
명령·입력·실제 출력·종료 코드는 `evidence/wo16/`에 기록했다.
최종 증거 커밋과 원격 HEAD는 최종 제출 기록에서 확인한다.

## 구현 결과
영수증 예약 근거, 소비 후 중단 조정, 멱등 조회, 상태·날짜 검증, 미완료 예약 충돌 차단을 구현했다.
정상 v6 Import·PasswordChange의 세대 CAS와 AnchorCommitPending 결과를 추가했다.
이전 인증 헤더를 이용한 합성 v5 사본 회수를 구현했다.
비밀번호 변경 제한 UI, 접근성 ID, GuiProbe 검사, 금고·앵커 정리 보호를 추가했다.
현재 DPAPI 공급자의 신뢰 범위는 확대하지 않았다.

## GitHub만으로 알 수 없는 실행 조건
로컬 Windows 11 build 26200, SDK 10.0.401, 논리 CPU 24개에서 실행했다.
실행 토큰은 관리자 역할 False였다. Codex 승인 재시도는 Windows UAC 승인 시험이 아니다.
초기 sandbox testhost·파일 교체·restore·Git Bash 오류는 승인된 일반 프로세스 재시도로 해결했다.
실제 사용자 파일과 금고는 열지 않았다. 모든 파일은 시험 코드가 생성한 합성 데이터다.
합성 금고와 비공개 코드 파일은 로컬 임시 시험 영역에 보존했다.
비공개 fixture 위치·복구 코드·비밀번호·평문·암호화 객체는 GitHub 증거에 포함하지 않았다.
GUI·독립 계정·VM 스냅샷·UAC·재부팅·물리 전원 차단·저사양 장비는 실행하지 않았다.

## 검증 집계
기준 앱 1108 PASS / 111 SKIP / 0 FAIL.
최종 앱 1139 PASS / 111 SKIP / 0 FAIL / 1250 total.
신규 WO16 31 PASS. 기존 Skip 111개 이름을 대조했다.
SecurityLab 36 PASS / 0 SKIP / 0 FAIL.
신규 별도 프로세스 경로: 영수증 9, 세대 4, v5 회수 2.
기존 WO15 별도 프로세스 회귀: 영수증 15, 레거시 비밀번호 6.
기존 레거시 2개 정상 잠금 실패는 유지된다. 이전 인증 헤더 사본을 제공한 별도 회수만 성공했다.
Restore·Release·GuiProbe 빌드 경고 0·오류 0.
실제 금고 복구는 추출 SHA-256으로 확인했다. 소스 복사본 롤백은 별도 해시 검사다.

## 변경 파일
- `.github/workflows/wo16-windows-validation.yml`
- `scripts/wo16-gui-validation.ps1`
- `scripts/wo16-ui-source-validation.ps1`
- `src/SmartPerformanceDoctor.App/Services/SystemCare/StorageInventory.cs`
- `src/SmartPerformanceDoctor.App/Views/SecureVaultCenterPage.xaml`
- `src/SmartPerformanceDoctor.App/Views/SecureVaultCenterPage.xaml.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabRecoverySlots.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabTrustedState.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabV5Transition.cs`
- `src/SmartPerformanceDoctor.SecurityLab/src/VaultV4/LabVaultService.TrustedRecovery.cs`
- `tests/SmartPerformanceDoctor.Tests/WO16VaultProtectionTests.cs`
- `tests/SmartPerformanceDoctor.Tests/WO16VaultStabilityTests.cs`
- `tools/SmartPerformanceDoctor.GuiProbe/Program.cs`
- `tools/SmartPerformanceDoctor.VaultProbe/Program.cs`
- `tools/SmartPerformanceDoctor.VaultProbe/StandardStabilityProbe.cs`
- `tools/SmartPerformanceDoctor.VaultProbe/TrustedRecoveryProbe.cs`

## 복원 절차
독립 소스 복사본은 ROLLBACK.sh와 ORIGINAL_FILE로 복원한다.
스크립트는 RESTORE_COPY 이름과 수정·원본 해시를 검사한다.
실행 예: `bash docs/rework/evidence/wo16/ROLLBACK.sh artifacts/wo16/wo16-rollback-validation/RESTORE_COPY`.
전체 구현 취소는 별도 작업 브랜치에서 검증 소스 커밋을 revert하고 회귀를 재실행한다.
금고 회수는 원본 보존 후 인증된 별도 후보 사본에서 수행한다.
소스 롤백은 금고 데이터 복구 성공의 근거가 아니다.

## 검증 소스 정합성 보정
솔루션 빌드는 테스트를 bin/Release에 출력했다.
x64 프로젝트 시험은 bin/x64/Release의 기존 바이너리를 사용했다.
첫 전체 집계는 1137 PASS였다. 마지막 추가 시험 2개가 포함되지 않았다.
x64 테스트·프로브를 명시적으로 재빌드했다.
최종 전체 집계는 1139 PASS / 111 SKIP / 0 FAIL이다.
최종 프로브와 스트리밍 측정도 해당 바이너리로 재실행했다.
초기 집계를 최종 검증으로 사용하지 않았다.

## Windows CI
Windows CI [38419949642](https://github.com/scarlel96-design/PC-Care/actions/runs/38419949642)는 SUCCESS다.
검증 소스는 `1ca80b98f63ec953c952647fcf28f8032ac986a7`이다.
원격 앱 1139 PASS / 111 SKIP / 0 FAIL, 신규 WO16 31 PASS, SecurityLab 36 PASS다.
CI의 전체 앱 집계에는 업데이트 128과 성능 233이 포함된다.
최종 증거 커밋은 문서와 .gitattributes만 추가한다.
CI Actions의 Node 20 선언에 대한 deprecation 안내가 있다. Runner는 Node 24를 사용했다.
이 안내는 C# 빌드 경고 0과 구분한다. Actions 버전 정리는 후속 P1이다.
