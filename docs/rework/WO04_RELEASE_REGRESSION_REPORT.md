# WO-04 Release 회귀와 신뢰 정책

작업 브랜치: `codex/pccare-rework-phase1-final-validation`
기준 커밋: `14b1a8151a742db577e92a586969a18625c06270`
검증 소스 커밋: `082a65ec55c1879d02ea807bd3efb04abc17c026`
Git 트리: `f883aa1475beb03ede8aacfb5bc679caae79602c`
Windows CI: `38033748493`.
최종 결과는 `evidence/wo04/CI_RESULT.txt`에 기록한다.
증거 갱신 커밋은 검증 소스 커밋과 구분한다.
실행 명령·입력·출력·종료 코드는 `evidence/wo04/VERIFICATION.txt`, `TEST_RESULT.txt`, `RECOVERY_TEST_RESULT.txt`, `BUILD_RESULT.txt`에 보존한다.


상태: PARTIAL.

| 환경 | 암호 검증 | 발행자 | 실행 정책 | 실제 증거 |
|---|---|---|---|---|
| Debug 개발, 무서명, ProgramFiles 밖 | Unsigned | Unknown | 개발 실행 Allowed | 정책 단위 테스트; Debug GUI NOT TESTED |
| Release 개발 시험 앱 | 무서명/발행자 미등록이면 차단 | Unknown/Untrusted | Blocked | Release DLL Get-AuthenticodeSignature=NotSigned; 실제 테스트용 Authenticode 앱 BLOCKED |
| Release 시험용 엔진 harness | ECDSA P-256 서명 검증 | ephemeral test policy | 합성 패키지 허용 | SignedUpdateTests 및 별도 프로세스 Probe |
| Release 생산 | 공식 인증서 pins/업데이트 키 목록 비어 있음 | Unknown | 중요 기능 Blocked | 소스·Release 빌드·정책 테스트, 생산 실행 BLOCKED |
| 변조 서명·임의 발행자 | Failed 또는 미신뢰 | Untrusted/Unknown | Blocked | 부정 패키지 자동 테스트 |

RuntimeTrustState, RuntimeAuthenticodeVerifier, RuntimeTrustPolicy, FeatureGateService, SmartProtectionDefaults를 검사했다.
SilentConsumerMode는 FeatureGate 우회가 아니다.
SecureVault, ProfessionalSecureDelete, RegistryDoctor, DriverAudioRepair, VulnerabilityFix, ProgramIntegrity의 실행은 서명·발행자 신뢰 미확인 Release에서 차단된다.
RulePack/Aegis 별도 gate도 유지된다.
UI 차단 메시지는 재설치가 해결책이라는 과장을 없애고 검증된 발행자 빌드 대기를 안내한다.

무서명 소비자 설치본에 대한 호환 정책: 진단·탐색·설정·업데이트 확인 등 기존 비파괴 경로는 각각의 gate 정책으로 유지한다.
여섯 중요 작업은 사용자 동의만으로 해제하지 않는다.
금고 복호화/내보내기 같은 데이터 회수 경로가 별도 신뢰 정책으로 제공되는지는 이 작업에서 실제 설치본으로 검증하지 않았다.
임의 승인으로 회수 기능을 활성화하지 않는다.
공식 소비자 호환 시험이 필요하다.

앱 전체 자동 회귀와 SecurityLab 결과는 TEST_RESULT.txt에 기록한다.
메인 화면 렌더링, 메뉴 탐색, 시스템 진단/케어 실사용, 금고/삭제 GUI, 설정 저장/앱 재실행/파일 picker의 설치본 동작은 NOT TESTED다.
자동 합성 보안 회귀는 해당 GUI·실사용을 대체하지 않는다.

## 자동 시험 군의 실제 연결

WO04의 시험 군은 별도 클래스 이름 대신 다음 기존 시험과 CI 단계로 구성했다.

| 요구 시험 군 | 실제 실행 대상 | 검증 범위 |
|---|---|---|
| SignedPackageTests | NegativePackagesFailClosed | 서명, 해시, allowlist 부정 입력 |
| PublisherPolicyTests | DevelopmentKeyIsRejectedByProductionPolicy | 시험 키의 생산 정책 거부 |
| UpdateTransactionTests | TransactionAppliesAllowlistAndPreservesUnlistedData | 정상 적용, 비대상 자료 유지 |
| UpdateRecoveryTests | RestartRecoveryReplaysIncompleteApplyingJournal, InterruptedRollbackCanBeRetried | 저널 복구, 복원 재시도 |
| ProtectedStagingTests | StagingTamperRollsBackOriginalAndNewFiles | 합성 스테이징 변조 거부 |
| ElevatedHelperTests | Reject unsupported helper verb CI 단계 | --exec arbitrary 종료 코드 2 |
| RuntimeTrustPolicyTests | RuntimeExecutionPolicyKeepsThreeTrustDimensionsSeparate | 신뢰 상태별 정책 결정 |
| ExistingSecurityRegressionTests | Full application regression, SecurityLab regression CI 단계 | 기존 자동 보안 회귀 |

ProtectedStagingTests는 실제 보호 ACL의 쓰기 거부를 증명하지 않는다.
ElevatedHelperTests는 UAC 승인과 거부를 실행하지 않는다.
발행자 정책 시험은 실제 Authenticode 인증서 시험을 대체하지 않는다.
앱 전체 111개 Skip은 통과 수에 포함하지 않는다.
