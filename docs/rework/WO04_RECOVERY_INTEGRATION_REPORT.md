# WO-04 복구 연결

작업 브랜치: `codex/pccare-rework-phase1-final-validation`
기준 커밋: `14b1a8151a742db577e92a586969a18625c06270`
검증 소스 커밋: `082a65ec55c1879d02ea807bd3efb04abc17c026`
Git 트리: `f883aa1475beb03ede8aacfb5bc679caae79602c`
Windows CI: `38033748493`.
최종 결과는 `evidence/wo04/CI_RESULT.txt`에 기록한다.
증거 갱신 커밋은 검증 소스 커밋과 구분한다.
실행 명령·입력·출력·종료 코드는 `evidence/wo04/VERIFICATION.txt`, `TEST_RESULT.txt`, `RECOVERY_TEST_RESULT.txt`, `BUILD_RESULT.txt`에 보존한다.


상태: PARTIAL.
엔진의 별도 프로세스 복구는 PASS, 실제 설치 앱 재시작 연결의 E2E는 BLOCKED.

앱 시작은 ProgramData 고정 위치를 읽기 전용 검사하고 signed-update-recovery 로그에 state/code/id를 남긴다.
미완료·손상·접근 거부 상태를 발견하면 설정 업데이트 화면을 안내한다.
해당 상태에서 레거시 pending applier를 실행하지 않는다.
새 패키지 검사는 복구 필요/실패/진행 중이면 RECOVERY-REQUIRED로 중단한다.

| 상태 | 의미와 사용자 동작 |
|---|---|
| NoPendingTransaction | 루트 없음 또는 Committed만 존재 |
| RecoveryAvailable | 유효한 미완료 저널, 관리자 복구 시작 |
| RecoveryInProgress | exclusive update.lock 사용 중, 복구 버튼 비활성 |
| RecoveryCompleted | 미완료가 없고 Restored 저널 존재, 복구 완료 표시 |
| RecoveryRequired | 손상/루트 불일치/접근 거부, 성공으로 표시하지 않음 |
| RecoveryFailed | 이전 rollback 실패 기록, 자료 보존 후 재시도 |

일반 앱은 고정 engine/SmartPerformanceDoctor.UpdateHelper.exe를 runas로 실행한다.
UAC 1223은 Cancelled로 유지한다.
helper 시작 후에만 앱을 종료한다.
helper는 관리자, ProgramFiles 하위, 고정 verb/설치 위치를 검증하고 호출 PID 종료를 기다린다.
실제 UAC 사용자 동작은 BLOCKED다.

저널은 tx-GUID N과 ID 일치, InstallRoot 정확 일치, enum, 최대4096 파일, 최대1MiB JSON, 안전한 payload 경로, case-insensitive 중복 금지, SHA256 형식, 원본 크기, reparse 백업 경로를 검사한다.
기존 WO03 저널처럼 InstallRoot가 없는 입력은 JOURNAL-INVALID로 보존한다.
관리자가 확인한 별도 마이그레이션 없이 자동 복원하지 않는다.
저널은 별도 암호 서명이 없고 보호 ACL에 의존한다.
strictAcl=false는 테스트/읽기 검사만이며 관리자 복원은 strictAcl=true다.

각 백업의 실제 해시와 대상 경로를 전부 확인한 후 복원한다.
새로 생긴 파일은 업데이트 해시와 일치할 때만 삭제한다.
복원 실패 후 저널과 백업을 보존한다.
소스 ROLLBACK.sh와 설치 Recover를 혼동하지 않는다.

FailFast는 첫 설치 파일 교체 후 worker를 실제 종료했고 별도 Recover 프로세스가 old 및 vault-marker를 확인했다.
Windows 재부팅·정전·앱 설치 실행을 시험한 것은 아니다.
synthetic-vault.txt는 문자열 표시 파일이며 실제 암호화 금고가 아니다.

저널 용량은 적용 전과 기록 시에 검사한다.
검증된 패키지도 복구 용량을 초과하면 설치 전에 거부한다.
한도는 4,096개 파일, 1 MiB JSON, 2,048자 오류 문자열이다.

최종 로컬 재검사에서는 FailFast 메시지 후 시험 프로세스가 대기했다.
PID와 합성 명령줄을 확인하고 해당 프로세스만 외부 종료했다.
별도 복구와 내용 확인은 종료 코드 0을 반환했다.
대기 원인은 UNVERIFIED다.
CI의 종료 동작은 CI_RESULT.txt에서 별도로 확인한다.

기존 저널의 InstallRoot 누락 또는 새 한도 초과는 자동 마이그레이션하지 않는다.
대형 기존 저널과 긴 오류 문자열의 호환성은 NOT TESTED다.
해당 자료는 보존하고 검토된 관리자 마이그레이션 절차를 마련해야 한다.
