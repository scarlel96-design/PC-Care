# WO-06 Authenticode 파일 동일성

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

Status: PARTIAL. 실제 Windows 서명 검증과 핸들 고정 시험은 PASS다.

## 구현

AuthenticodeBoundary는 파일과 부모 디렉터리 핸들을 유지한다.
열기는 재분석 지점과 하드링크를 거부한다.
읽기 공유만 허용하여 쓰기와 삭제를 차단한다.
검증 전후에 파일 ID, 볼륨 ID와 SHA-256을 확인한다.
WinVerifyTrust에 열린 hFile과 경로를 제공한다.
인증서는 같은 WinTrust provider state에서 추출한다.
인증서 추출을 위해 경로를 다시 열지 않는다.
VERIFY로 만든 상태는 finally의 CLOSE로 해제한다.
성공 코드, provider signer 상태, 인증서와 동일성 검사 실패는 미검증으로 처리한다.
발행자 신뢰는 컴파일된 인증서 thumbprint 집합으로 결정한다.
Subject 문자열은 신뢰 근거로 사용하지 않는다.

RuntimeAuthenticodeVerifier는 이 경계를 사용한다.
공식 생산 인증서 목록은 비어 있다.
생산 Release의 보호 기능을 허용하기 위해 Verified를 임의 생성하지 않았다.
환경 변수로 신뢰 게이트를 해제하지 않았다.
테스트 thumbprint는 PCCARE_VM_TEST 빌드에만 들어간다.

## 실제 실행

로컬 Program Files/dotnet/dotnet.exe를 독립 시험 디렉터리에 복사했다.
실제 내장 Microsoft 서명은 WinVerifyTrust 성공 코드 0을 반환했다.
검증 중 쓰기와 삭제 시도는 IOException으로 차단됐다.
볼륨과 파일 ID 문자열 길이 49, SHA-256 길이 64를 확인했다.
Microsoft 서명은 PC-Care 발행자 핀에 없으므로 승인하지 않았다.
PE 바이트 변경 후 서명 검증은 실패했다.
무서명 엔진, 누락 파일과 외부 부트스트랩 경로를 거부했다.

초기 WindowsPowerShell.exe 복사본은 내장 서명 검증 조건에 맞지 않았다.
실제 내장 서명이 있는 dotnet.exe로 시험을 수정했다.
카탈로그 서명 지원과 정책을 이 시험으로 증명하지 않았다.
인증서 추출 API 오류, 정션 교체와 모든 경쟁 타이밍의 개별 주입은 NOT TESTED다.
기존 파일 경계 회귀 시험은 별도로 유지했다.
공식 PC-Care 생산 서명 파일은 BLOCKED다.

## API 근거

[WINTRUST_FILE_INFO](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/ns-wintrust-wintrust_file_info)는 hFile을 정의한다.
[WINTRUST_DATA](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/ns-wintrust-wintrust_data)는 검증 상태 핸들을 정의한다.
[WTHelperGetProvSignerFromChain](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/nf-wintrust-wthelpergetprovsignerfromchain)는 provider state의 서명자를 읽는다.
보존된 핸들과 동일 provider state는 기존 경로 재읽기 간격을 제거한다.
이 결과는 커널과 관리자 공격에 대한 절대 보장을 뜻하지 않는다.

## 검증 및 복원

실제 명령, 입력, 출력, 종료 코드는 `evidence/wo06/VERIFICATION.txt`에 기록했다.
개별 프로세스 121개 명령은 `evidence/wo06/PROCESS_COMMANDS.json`에 기록했다.
소스 복원은 표시된 독립 복사본에 `SourceRollback.ps1` 또는 `ROLLBACK.sh`를 실행한다.
설치 복원은 인증된 부트스트랩의 `--recover`를 실행한다.
소스 해시 복원과 설치 트랜잭션 복원은 별도 결과다.
실제 사용자 설치본과 금고는 시험에 사용하지 않았다.
