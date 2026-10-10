# WO-06 자체 업데이트 검증

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

Status: PARTIAL.

## 실행 결과

| 시험 | 결과 | 증거 범위 |
|---|---|---|
| 실행 중인 EXE 교체 | PASS | 합성 UpdateRecoveryProbe 이미지. 교체 실패 시 기존 파일 유지 |
| DLL 핸들 잠금 | PASS | 읽기 전용 공유 핸들. 파일 적용 차단 |
| 잠금 해제 후 복구와 재적용 | PASS | 별도 Recover 프로세스, 원본 복원, 재적용 해시 확인 |
| 적용 중 종료 | PASS | 합성 프로세스 강제 종료. 별도 복구 |
| 복원 중 종료 | PASS | 다음 복구 프로세스가 원본 상태 확인 |
| 외부 도우미 경로 | PASS | 임시 경로 실행 요청 거부 |
| 인증된 실제 도우미 자체 전환 | BLOCKED | 전용 VM과 테스트 서명 설치본 미제공 |
| 새 실제 도우미 손상 후 이전 스냅샷 실행 | NOT TESTED | 코드 경로 존재. 실행 증거 없음 |
| Windows 재부팅 후 자체 복구 | BLOCKED | 전용 VM 미제공 |

프로세스 매트릭스는 121개 명령을 실행했다.
81개 종료 코드 0, 37개 강제 종료 -1, 3개 예상 실패 5를 기록했다.
예상 실패는 실행 이미지 잠금, DLL 잠금, ID 없는 임시 파일 복구다.
실행 이미지 시험은 실제 PC-Care 도우미의 인증된 스냅샷 실행을 대체하지 않는다.

## 발견 및 수정

새 파일 이름 변경 버퍼에 UTF-16 종료 공간이 없었다.
이 오류는 이름 뒤에 임의 문자를 추가했다.
버퍼 길이에 종료 공간을 추가하고 전체 버퍼를 초기화했다.
최종 새 파일 해시 확인과 별도 프로세스 복구가 통과했다.
초기 FailFast 시험은 Windows 오류 보고에서 대기했다.
시험 프로세스의 Kill을 사용하여 관리 예외 정리 없이 종료하도록 변경했다.
초기 잠금 시험은 미완료 저널을 복구하지 않고 다시 적용했다.
엔진은 PRIOR-TRANSACTION으로 차단했다.
하네스에 Recover를 추가한 뒤 재적용이 통과했다.

실제 사용자 앱 실행, GUI, 설치기와 UAC 시험은 NOT TESTED 또는 BLOCKED다.

## 검증 및 복원

실제 명령, 입력, 출력, 종료 코드는 `evidence/wo06/VERIFICATION.txt`에 기록했다.
개별 프로세스 121개 명령은 `evidence/wo06/PROCESS_COMMANDS.json`에 기록했다.
소스 복원은 표시된 독립 복사본에 `SourceRollback.ps1` 또는 `ROLLBACK.sh`를 실행한다.
설치 복원은 인증된 부트스트랩의 `--recover`를 실행한다.
소스 해시 복원과 설치 트랜잭션 복원은 별도 결과다.
실제 사용자 설치본과 금고는 시험에 사용하지 않았다.
