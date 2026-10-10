# WO-04 트랜잭션 장애 매트릭스

작업 브랜치: `codex/pccare-rework-phase1-final-validation`  
기준 커밋: `14b1a8151a742db577e92a586969a18625c06270`  
최종 검증 소스 커밋·트리·CI 실행 번호: `evidence/wo04/SOURCE_IDENTITY.txt` 및 `CI_RESULT.txt` 참조. 증거 갱신 커밋은 검증 소스 커밋과 구분한다.  
실행 명령·입력·출력·종료 코드는 `evidence/wo04/VERIFICATION.txt`, `TEST_RESULT.txt`, `RECOVERY_TEST_RESULT.txt`, `BUILD_RESULT.txt`에 보존한다.

상태: PARTIAL. 14개 인위적 예외 지점을 자동 검사한다. 실제 정전·Windows 재부팅 시험은 BLOCKED다.

| 번호 | 주입 지점 | 불변 조건 | 결과 |
|---|---|---|---|
| 1 | AfterPackageVerification | 원본 old 유지; 신규 second 없음 | TEST_RESULT 참조 |
| 2 | AfterStagingCreated | 원본 old 유지; 신규 second 없음 | TEST_RESULT 참조 |
| 3 | DuringStageExtraction | 원본 old 유지; 신규 second 없음 | TEST_RESULT 참조 |
| 4 | DuringBackupCreation | 원본 old 유지; 신규 second 없음 | TEST_RESULT 참조 |
| 5 | AfterBackupVerification | 원본 old 유지; 신규 second 없음 | TEST_RESULT 참조 |
| 6 | AfterApplyingJournal | 원본 old 유지; 신규 second 없음 | TEST_RESULT 참조 |
| 7 | AfterFirstReplacement | 원본 old 유지; 신규 second 없음 | TEST_RESULT 참조 |
| 8 | BeforeReplacement | 원본 old 유지; 신규 second 없음 | TEST_RESULT 참조 |
| 9 | AfterLastReplacement | 원본 old 유지; 신규 second 없음 | TEST_RESULT 참조 |
| 10 | DuringPostApplyVerification | 원본 old 유지; 신규 second 없음 | TEST_RESULT 참조 |
| 11 | BeforeCommittedJournal | 원본 old 유지; 신규 second 없음 | TEST_RESULT 참조 |
| 12 | AfterRollbackStarted | RecoveryRequired/Failed, 재실행 후 old 복원 | TEST_RESULT 참조 |
| 13 | DuringRollbackCopy | RecoveryRequired/Failed, 재실행 후 old 복원 | TEST_RESULT 참조 |
| 14 | BeforeRestoredJournal | RecoveryRequired/Failed, 재실행 후 old 복원 | TEST_RESULT 참조 |

추가 검사: ID/설치 루트/경로/해시/누락 루트/중복/null Files, 과대 JSON, 손상 백업 전체 사전 검증, exclusive lock, 하드링크 외부파일, 예약이름·ADS·traversal. 별도 프로세스는 첫 교체 지점에서 Environment.FailFast로 실제 종료한다. exception matrix를 재부팅 검증이라고 부르지 않는다.
