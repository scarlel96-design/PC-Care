# WO-15 v5 비밀번호 중단

작업 브랜치: `codex/pccare-vault-freshness-recovery-receipts`.
기준 HEAD: `b99d29f1591cb46ce56b299665648e486072d4b8`. 기준 트리: `56f048f3465316147a6bc52b1acb732b8c6825d1`.
검증 소스: `ab2c19f3027a5450a55042915af69220f7ec5ccc`. 소스 트리: `37233729e49b78d1b1dc4bbbe3d5416db72a15e0`.
환경: Windows 11 build 26200, .NET SDK 10.0.401, 논리 CPU 24개.
입력: 합성 LabFast fixture. 실제 사용자 금고와 설치본은 시험에 사용하지 않았다.
판정: **PARTIAL / Phase 1 NO-GO**.
명령과 입력·출력·종료 코드는 `evidence/wo15/`의 항목별 기록에서 확인한다.

## 원인과 차단
기존 v5는 헤더·슬롯·Commit 정보를 여러 번 갱신한다.
PasswordHeadersWritten과 PasswordSlotsWritten에서 종료하면 두 비밀번호의 정상 잠금 해제가 실패한다.
암호화 pack 바이트는 보존됐다. 인증된 추출 실패 상태는 PreservedButLocked다.
공개 v4/v5 ChangePassword를 차단했다. 기존 비밀번호와 원본 파일을 보존한다.
기존 위험 재현은 internal v5 합성 fixture 전용 경로에 남겼다.
이미 손상된 과거 v5의 비밀번호 복구를 완료했다고 주장하지 않는다.

| 종료 지점 | 이전 비밀번호 바이트 | 새 비밀번호 바이트 | pack 보존 |
|---|---|---|---|
| LegacyPasswordBeforeHeader | True | False | True |
| LegacyPrimaryHeaderWritten | True | False | True |
| LegacySecondaryHeaderWritten | False | True | True |
| LegacyPasswordHeadersWritten | False | False | True |
| LegacyPasswordSlotsWritten | False | False | True |
| LegacyPasswordCommitted | False | True | True |

전환된 v6의 기존 비밀번호 중단 10개 지점은 재시험을 통과했다.
MetadataReady v5의 정상 잠금 해제는 exit 4다.
읽기 전용 salvage에서 인증된 3개 파일을 회수했다. 원본 파일 해시는 변경되지 않았다.
salvage의 Commit 이력은 Qualified가 아니다. 정상 금고 복원과 구분한다.
실제 과거 버전 바이너리와 생산 KDF 전환은 NOT TESTED다.
