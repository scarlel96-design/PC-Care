# WO-06 저널 내구성

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

Status: PARTIAL. 프로세스 종료 복구는 PASS다. 전원 손실 내구성은 BLOCKED다.

## 기록 프로토콜

새 저널은 고유 이름의 .new 파일을 CreateNew로 연다.
기록은 WriteThrough와 Flush(true)를 사용한다.
기존 저널은 File.Replace로 교체한다. journal.previous.json에 이전 세대를 보존한다.
최초 저널은 File.Move로 게시한다.
이전 세대는 하나만 유지한다.
ReplaceFile의 원자 교체와 저장장치 영속성은 같은 의미가 아니다.
지원되지 않는 REPLACEFILE_WRITE_THROUGH 플래그는 사용하지 않는다.
디렉터리 메타데이터 Flush와 전원 손실 순서 보장은 증명하지 않았다.

주 저널이 없거나 JSON 문법이 잘렸으면 이전 세대를 읽는다.
이전 세대는 설치 ID, 파일 ID, ACL, 경로, 백업과 실제 콘텐츠를 다시 검사한다.
주 저널의 의미 검증 실패는 이전 세대로 우회하지 않는다.
중복 필드, 위조 ID와 알 수 없는 필드는 차단한다.
유효한 이전 세대가 없으면 파일을 임의 복원하거나 삭제하지 않는다.
완료 기록이 손상되면 이전 Applying 세대로 보수적 복원이 발생할 수 있다.

## 장애 주입

임시 생성, 부분 기록, Flush, 교체 직전, 교체 직후에 예외를 주입했다.
같은 지점에서 별도 프로세스 강제 종료도 실행했다.
Committed 기록 직전 강제 종료와 롤백 중 재진입도 실행했다.
주 저널 삭제와 잘린 JSON은 이전 세대를 검증한 뒤 복원했다.
위조 ID가 있는 유효 JSON은 RecoveryRequired를 유지했다.
Windows 재부팅, VM 전원 차단, 물리 장치 정전 시험은 BLOCKED다.
RollingBack과 Restored 기록의 임시 생성, 부분 쓰기, Flush, 교체 전후 종료를 각각 시험했다.
별도 재실행 복구는 기존 파일과 합성 금고 표식 보존을 확인했다.

## API 근거

[ReplaceFileW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew)는 이전 파일 백업을 지원한다.
문서는 REPLACEFILE_WRITE_THROUGH가 지원되지 않는다고 명시한다.
현재 구현은 파일 Flush와 이전 세대를 결합한다.
이 설계가 실제 정전을 모두 견딘다는 주장은 하지 않는다.

## 검증 및 복원

실제 명령, 입력, 출력, 종료 코드는 `evidence/wo06/VERIFICATION.txt`에 기록했다.
개별 프로세스 121개 명령은 `evidence/wo06/PROCESS_COMMANDS.json`에 기록했다.
소스 복원은 표시된 독립 복사본에 `SourceRollback.ps1` 또는 `ROLLBACK.sh`를 실행한다.
설치 복원은 인증된 부트스트랩의 `--recover`를 실행한다.
소스 해시 복원과 설치 트랜잭션 복원은 별도 결과다.
실제 사용자 설치본과 금고는 시험에 사용하지 않았다.
