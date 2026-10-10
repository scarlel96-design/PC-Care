# WO-06 구형 저널 호환성

작업 번호: WO-06
작업 브랜치: `codex/pccare-rework-phase1-bootstrap-recovery`
기준 커밋: `291f84daf1e950ba9cb74af0c8fea8879a11174e`
기준 트리: `33d084c1253a5b042c158600086db263f09faf89`
검증 소스 커밋: `5647b352cf41082def74bef2701b6fe707c51cc0`
검증 소스 트리: `e2e5452f31d8550050d7560899f409486e10dbb1`
최종 증거 커밋: 검증 소스 이후의 docs 전용 커밋. 실제 HEAD는 최종 Git 영수증을 참조한다.
실행 경로: `C:\Users\scarl\Documents\Codex\2026-07-18\c-users-scarl-desktop-pccare-pc\work\PC-Care`
실행 환경: Windows 11 Pro 10.0.26200, x64, NTFS Healthy, .NET SDK 10.0.401, runtime 10.0.12.
로컬 토큰: Administrator=False. 전용 VM과 스냅샷은 미제공이다.
CI: [38054469100](https://github.com/scarlel96-design/PC-Care/actions/runs/38054469100). 정확한 결과는 `evidence/wo06/GITHUB_RESULT.txt`를 참조한다.

Status: PARTIAL. 완료 기록 이관과 미완료 기록 보존 검사는 PASS다.

## 상태 처리

| Schema 1 상태 | 처리 |
|---|---|
| Committed | 경로, 원본 저널, 설치 콘텐츠, 백업 해시를 확인한다. 읽기 전용 영수증을 추가한다 |
| Restored | 복원된 콘텐츠와 새 파일 부재를 확인한다. 읽기 전용 영수증을 추가한다 |
| Prepared | 원본 보존. RecoveryRequired |
| Applying | 원본 보존. RecoveryRequired |
| RollbackPending | 원본 보존. RecoveryRequired |
| RollingBack | 원본 보존. RecoveryRequired |
| 손상, 경로 불일치, 위조 ID, null 필드 | 거부. 자동 파일 쓰기 권한을 부여하지 않는다 |

완료 영수증은 원본 저널 해시, 설치 디렉터리 ID와 트랜잭션 디렉터리 ID를 결합한다.
원본 저널은 변경하지 않는다.
영수증은 CreateNew, Flush, 이름 변경을 사용한다.
기존 영수증과 원본 해시가 일치하면 후속 업데이트가 이전 완료 기록에 차단되지 않는다.
영수증은 구형 저널을 새로운 복구 명령으로 승격하지 않는다.
보호 모드의 ACL 검증이 출처 경계를 제공한다.
합성 모드의 영수증 시험은 실제 보호 ACL 증명이 아니다.

## 자동 시험

Committed와 Restored 각각 이관, 두 차례 후속 적용, 원본 해시 보존을 확인했다.
네 미완료 상태는 동일 원본 해시와 기존 설치 콘텐츠를 유지했다.
영수증 이후 원본 변경은 RecoveryRequired로 거부했다.
외부 설치 경로, null ID, null 파일 항목을 거부했다.
Schema 2의 위조 ID는 이전 세대 저널로 우회하지 않는다.
구형 기록과 후속 신형 저널의 공존을 시험했다.
영수증 생성 도중 별도 프로세스 강제 종료는 NOT TESTED다.

## 관리자 진단 절차

1. 중단된 설치 디렉터리와 ProgramData 트랜잭션을 스냅샷으로 보존한다.
2. 원본 저널과 각 파일의 SHA-256을 기록한다.
3. 설치 루트, 트랜잭션 위치, ACL과 재분석 지점을 확인한다.
4. 백업 해시를 원본 해시와 대조한다.
5. 현재 설치 파일을 패키지 허용 목록과 대조한다.
6. 신뢰 조건을 확인한 뒤 별도 관리자 복구 도구를 검토한다.
7. 신뢰 조건이 부족하면 RecoveryRequired를 유지한다.

구형 미완료 저널의 상세 진단 API와 CLI를 추가했다. 실제 관리자 복구는 개별 증거 검토가 필요하다.
진단은 READONLY-MANUAL-REVIEW와 AUTOMATIC-RECOVERY-DENIED를 반환한다. 자동 복구 차단은 유지한다.

## 검증 및 복원

실제 명령, 입력, 출력, 종료 코드는 `evidence/wo06/VERIFICATION.txt`에 기록했다.
개별 프로세스 121개 명령은 `evidence/wo06/PROCESS_COMMANDS.json`에 기록했다.
소스 복원은 표시된 독립 복사본에 `SourceRollback.ps1` 또는 `ROLLBACK.sh`를 실행한다.
설치 복원은 인증된 부트스트랩의 `--recover`를 실행한다.
소스 해시 복원과 설치 트랜잭션 복원은 별도 결과다.
실제 사용자 설치본과 금고는 시험에 사용하지 않았다.

## 추가된 읽기 전용 진단

UpdateTransaction.DiagnoseLegacy는 원본 저널 해시와 디렉터리 ID를 측정한다.
파일 관찰은 ORIGINAL-MATCH, UPDATED-MATCH, UNKNOWN-CONTENT, MISSING과 BOUNDARY-REJECTED를 구분한다.
백업 관찰은 ORIGINAL-MATCH, HASH-MISMATCH, MISSING과 BOUNDARY-REJECTED를 구분한다.
잘못된 설치 루트와 식별자 필드는 READONLY-DIAGNOSIS-REJECTED로 거부한다.
측정한 ID는 현재 관찰값이다. 구형 파일의 생성 출처 증명으로 사용하지 않는다.
진단 결과는 복원 또는 삭제 권한이 아니다.
파일별 관찰은 단일 시점 스냅샷을 보장하지 않는다. 업데이트를 중지한 상태에서 관리자 검토를 수행한다.
네 미완료 상태와 손상된 백업, 외부 설치 루트의 6개 추가 시험이 통과했다.
시험은 원본 저널 및 작업 파일 해시 보존과 자동 복구 차단을 확인했다.
보호 모드 CLI는 아래 명령을 제공한다.
```powershell
SmartPerformanceDoctor.VMTestKit.exe diagnose-legacy "C:\Program Files\PC-Care-WO05-Lab"
```
CLI는 고정 ProgramData 작업 경로와 설치 ACL을 검증한다.
실제 보호 VM에서 이 CLI 실행은 BLOCKED다. 합성 자동 시험은 API를 검증했다.
