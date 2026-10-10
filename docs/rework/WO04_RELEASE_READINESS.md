# WO-04 Phase 1 출시 판정

작업 브랜치: `codex/pccare-rework-phase1-final-validation`
기준 커밋: `14b1a8151a742db577e92a586969a18625c06270`
검증 소스 커밋: `082a65ec55c1879d02ea807bd3efb04abc17c026`
Git 트리: `f883aa1475beb03ede8aacfb5bc679caae79602c`
Windows CI: `38033748493`.
최종 결과는 `evidence/wo04/CI_RESULT.txt`에 기록한다.
증거 갱신 커밋은 검증 소스 커밋과 구분한다.
실행 명령·입력·출력·종료 코드는 `evidence/wo04/VERIFICATION.txt`, `TEST_RESULT.txt`, `RECOVERY_TEST_RESULT.txt`, `BUILD_RESULT.txt`에 보존한다.


최종 판정: NO-GO.
전체 상태: PARTIAL.

| 조건 | 상태 | 근거/다음 증거 |
|---|---|---|
| 기준 커밋·트리 정합성 | PASS | 실제 Git 객체 동일 트리 |
| Release 게이트 원인/영향 | PASS | pins/키 비어 있음, 여섯 기능 gate, NotSigned |
| 신뢰 정책 자동 시험 | PASS | unsigned 개발 허용과 생산 차단 분리 |
| 앱 재시작 복구 연결 | PARTIAL | 코드 연결, 실제 설치 앱 E2E 필요 |
| 강제 종료·별도 복구 | PASS: 합성 프로세스 | synthetic FailFast/별도 process |
| 14 장애 지점 | PASS: 합성 자동 시험 | in-process exception matrix |
| 보호 ACL·UAC·재부팅 | BLOCKED | 전용 VM/스냅샷 미제공 |
| handle identity/TOCTOU | PARTIAL | 경로 검사와 실제 open 사이 경쟁 잔존 |
| 기존 금고·삭제 자동 회귀 | PASS: 합성 자동 시험 | 설치본 실사용 NOT TESTED |
| Windows CI | PASS | run38033748493, source082a65e |
| 공식 roots/개인키 보관·회전 | BLOCKED | 실제 release provision 필요 |
| 생산 서명 패키지/실설치 업데이트 | BLOCKED | 공식 생산 검증 미실시 |
| 무서명 이전 설치본 데이터 회수 호환 | NOT TESTED | 검토된 회수 정책+VM 필요 |

출시를 막는 후속 작업: 검토된 생산 발행자 roots와 키 운영, VM 전용 테스트 Release 신뢰 주입, UAC 승인·거부와 재부팅16개 E2E, 보호 ACL 실측, 경로 핸들 및 파일 식별자 검증, 기존 사용자 합성 데이터 호환성.
자동 테스트가 모두 통과해도 이 표의 BLOCKED/PARTIAL 항목은 그대로 유지한다.
main 병합·공식 릴리스는 수행하지 않는다.
