# WO-06 신규 파일 복구

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

Status: PARTIAL. 기록된 ID가 있는 파일의 복원은 PASS다.

## 상태 전이

1. 고유한 .pccare-TX-GUID.pending 경로를 저널에 먼저 기록한다.
2. CreateNew로 임시 파일을 생성한다.
3. 열린 파일의 ID를 저널에 기록한다.
4. 검증된 콘텐츠를 기록하고 Flush한다.
5. 해시를 확인한 뒤 TemporaryReady를 기록한다.
6. 유지한 핸들로 같은 부모의 최종 이름에 변경한다.
7. Applied 상태를 기록한다.

최종 이름 변경은 기존 파일을 덮어쓰지 않는다.
기존 사용자 파일과 이름이 충돌하면 중단한다.
복원은 기록된 ID가 일치하는 임시 파일만 처리한다.
TemporaryReady 파일은 패키지 해시까지 확인한다.
모든 복원 대상과 백업의 사전 검증이 끝난 뒤 기존 파일을 변경한다.
의심스러운 잔여물은 자동 삭제하지 않는다.

## 실행 결과와 한계

생성 의도 기록 직후, ID 기록 직후, 이름 변경 직전과 직후 중단은 복원됐다.
파일 생성 직후 ID 기록 전 종료는 임시 파일을 남긴다.
고유한 이름만으로 해당 파일의 출처를 증명하지 않는다.
이 경우 기존 콘텐츠를 유지한다. Recover는 RecoveryRequired 단계를 반환한다.
InspectRecovery는 RecoveryFailed 상태를 표시한다. 새 업데이트는 차단된다.
다음 실행에서도 같은 상태를 유지한다.
이 창은 최종 파일 이름에서 트랜잭션 전용 임시 이름으로 이동했다.
자동 복구가 완성됐다는 판정은 하지 않는다.

P0 잔여: 파일 생성과 신뢰 가능한 ID 영속화 사이의 미기록 객체 창.
후속 작업은 생성 객체의 출처를 증명하는 프로토콜과 독립적인 관리자 진단을 구현해야 한다.
저널 문자열만을 삭제 권한으로 사용하면 안 된다.
임시 파일 변경, 삭제, 하드링크와 이름 충돌의 모든 조합은 추가 VM 시험이 필요하다.

## 검증 및 복원

실제 명령, 입력, 출력, 종료 코드는 `evidence/wo06/VERIFICATION.txt`에 기록했다.
개별 프로세스 121개 명령은 `evidence/wo06/PROCESS_COMMANDS.json`에 기록했다.
소스 복원은 표시된 독립 복사본에 `SourceRollback.ps1` 또는 `ROLLBACK.sh`를 실행한다.
설치 복원은 인증된 부트스트랩의 `--recover`를 실행한다.
소스 해시 복원과 설치 트랜잭션 복원은 별도 결과다.
실제 사용자 설치본과 금고는 시험에 사용하지 않았다.
