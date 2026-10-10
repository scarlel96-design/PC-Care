# WO-04 Windows VM E2E

작업 브랜치: `codex/pccare-rework-phase1-final-validation`
기준 커밋: `14b1a8151a742db577e92a586969a18625c06270`
검증 소스 커밋: `082a65ec55c1879d02ea807bd3efb04abc17c026`
Git 트리: `f883aa1475beb03ede8aacfb5bc679caae79602c`
Windows CI: `38033748493`.
최종 결과는 `evidence/wo04/CI_RESULT.txt`에 기록한다.
증거 갱신 커밋은 검증 소스 커밋과 구분한다.
실행 명령·입력·출력·종료 코드는 `evidence/wo04/VERIFICATION.txt`, `TEST_RESULT.txt`, `RECOVERY_TEST_RESULT.txt`, `BUILD_RESULT.txt`에 보존한다.


상태: BLOCKED.

전용 Windows11 VM, 복구 스냅샷, 시험 설치본/전용 인증서가 제공되지 않았다.
vmrun, VBoxManage, qemu-system-x86_64, Get-VM은 발견되지 않았다.
사용자 호스트에 UAC를 띄우거나 재부팅·실제 설치본 교체·실금고 접근을 하지 않았다.

시험용 ECDSA 키는 SignedUpdateTests/UpdateRecoveryProbe에서만 생성한다.
Probe 개인키는 메모리에만 존재하고 공개키만 synthetic fixture에 저장한다.
일반 생산 Release의 키 목록에 등록하지 않는다.
실제 앱·helper에 테스트 신뢰 루트를 컴파일 시 주입하는 전용 VM Release 빌드는 아직 구현/실행되지 않았다.
따라서 engine harness를 실제 설치본 E2E라고 간주하지 않는다.

## 실행 가능한 하네스

UpdateRecoveryProbe의 prepare/apply-kill/recover/verify는 signed v2 생성, 첫 교체 후 강제 종료, 별도 프로세스 복원, 합성 표시 파일 유지 확인을 수행한다.
실행 명령과 실제 결과는 RECOVERY_TEST_RESULT.txt에 있다.
strictAcl=false 합성 경로이며 UAC/보호 ProgramData/실제 WinUI 앱을 실행하지 않는다.

VM 수집은 scripts/wo04-vm-evidence.ps1 -InstallRoot <VM 전용 설치> -EvidenceDirectory <VM 전용 증거> -SnapshotId <복구 가능한 스냅샷 ID> -Phase before 또는 after다.
증거 폴더 .wo04-disposable-vm marker를 먼저 만든다.
수집 스크립트는 설치·삭제·재부팅·UAC를 수행하지 않는다.

## 수동 시나리오와 합격 조건

1. 스냅샷을 만든 폐기 VM에 기존 설치본, 전용 합성 설정/금고를 준비하고 before hash manifest를 수집한다.
2. 정상 앱 실행과 버전/주요 메뉴를 확인한다.
3. 검토된 테스트 전용 빌드에만 시험 공개키를 주입하고 시험 signed package를 생성한다.
4. 패키지 검증 로그와 발행자 ID를 확인한다.
5. UAC 승인 후 정상 설치를 실행한다.
6. 설치 후 앱 실행, 버전·allowlist 파일 hash·합성 설정/금고 hash를 확인한다.
7. 스냅샷 복원 후 UAC 거부를 시험하고 앱 유지 및 pending 문구를 확인한다.
8. 파일 잠금 상태에서 적용하고 원본 hash 보존과 RecoveryRequired를 확인한다.
9. 첫/마지막 교체, 결과 기록 전, 복원 복사 중 helper를 종료한다.
10. 별도 앱 재시작과 VM Windows 재부팅을 각각 수행하고 탐지→사용자 승인→관리자 복원→정상 앱 실행을 확인한다.
11. 적용과 복구 동시 실행, 손상 저널/백업, 정션 교체를 시험하고 임의 파일 변경 없음과 증거 보존을 확인한다.
12. after manifest/ACL/이벤트/화면/UAC 승인 또는 거부/수동 수행자와 시간을 기록한다.

WO04 6.2의 16개 실제 설치 시나리오는 모두 BLOCKED다.
현재 PASS는 engine 합성 별도 프로세스 시험에만 해당한다.
