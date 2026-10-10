# WO-04 트랜잭션 장애 매트릭스

작업 브랜치: `codex/pccare-rework-phase1-final-validation`
기준 커밋: `14b1a8151a742db577e92a586969a18625c06270`
검증 소스 커밋: `082a65ec55c1879d02ea807bd3efb04abc17c026`
Git 트리: `f883aa1475beb03ede8aacfb5bc679caae79602c`
Windows CI: `38033748493`.
최종 결과는 `evidence/wo04/CI_RESULT.txt`에 기록한다.
증거 갱신 커밋은 검증 소스 커밋과 구분한다.
실행 명령·입력·출력·종료 코드는 `evidence/wo04/VERIFICATION.txt`, `TEST_RESULT.txt`, `RECOVERY_TEST_RESULT.txt`, `BUILD_RESULT.txt`에 보존한다.

상태: PARTIAL.
14개 인위적 예외 지점을 자동 검사한다.
실제 정전·Windows 재부팅 시험은 BLOCKED다.

| 번호 | 주입 지점 | 불변 조건 | 결과 |
|---|---|---|---|
| 1 | AfterPackageVerification | 원본 old 유지; 신규 second 없음 | PASS: 합성 자동 시험 |
| 2 | AfterStagingCreated | 원본 old 유지; 신규 second 없음 | PASS: 합성 자동 시험 |
| 3 | DuringStageExtraction | 원본 old 유지; 신규 second 없음 | PASS: 합성 자동 시험 |
| 4 | DuringBackupCreation | 원본 old 유지; 신규 second 없음 | PASS: 합성 자동 시험 |
| 5 | AfterBackupVerification | 원본 old 유지; 신규 second 없음 | PASS: 합성 자동 시험 |
| 6 | AfterApplyingJournal | 원본 old 유지; 신규 second 없음 | PASS: 합성 자동 시험 |
| 7 | AfterFirstReplacement | 원본 old 유지; 신규 second 없음 | PASS: 합성 자동 시험 |
| 8 | BeforeReplacement | 원본 old 유지; 신규 second 없음 | PASS: 합성 자동 시험 |
| 9 | AfterLastReplacement | 원본 old 유지; 신규 second 없음 | PASS: 합성 자동 시험 |
| 10 | DuringPostApplyVerification | 원본 old 유지; 신규 second 없음 | PASS: 합성 자동 시험 |
| 11 | BeforeCommittedJournal | 원본 old 유지; 신규 second 없음 | PASS: 합성 자동 시험 |
| 12 | AfterRollbackStarted | RecoveryRequired/Failed, 재실행 후 old 복원 | PASS: 합성 자동 시험 |
| 13 | DuringRollbackCopy | RecoveryRequired/Failed, 재실행 후 old 복원 | PASS: 합성 자동 시험 |
| 14 | BeforeRestoredJournal | RecoveryRequired/Failed, 재실행 후 old 복원 | PASS: 합성 자동 시험 |

추가 검사: ID/설치 루트/경로/해시/누락 루트/중복/null Files, 과대 JSON, 손상 백업 전체 사전 검증, exclusive lock, 하드링크 외부파일, 예약이름·ADS·traversal.
별도 프로세스는 첫 교체 지점에서 Environment.FailFast로 실제 종료한다.
exception matrix를 재부팅 검증이라고 부르지 않는다.

추가 경계값: 4,097개 파일과 4,096개 긴 경로 패키지를 각각 거부했다.
두 패키지는 암호학적 검증에 통과한 입력이다.
설치 파일은 old를 유지했다.
새 트랜잭션 디렉터리는 생성되지 않았다.
null 오류 문자열과 2,049자 오류 문자열 저널도 거부했다.
