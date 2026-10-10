# WO-06 Windows VM 설치 검증

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

Status: BLOCKED. 실제 VM 설치, UAC, 재부팅 E2E는 실행하지 않았다.

## 제공된 자동화

scripts/wo06-vm-harness.ps1은 WO-05 VM 자산을 재사용한다.
필수 marker, SnapshotId와 VM 제조사 모델을 확인한다.
전용 설치 디렉터리는 PC-Care-WO05-Lab이다.
준비 단계는 VM 전용 NonExportable Authenticode 키와 ECDSA 패키지 키를 만든다.
패키지 개인 키 파일은 VM의 제한된 trust 디렉터리에만 둔다.
이 파일은 평문 PKCS#8이다. VM 폐기와 ACL 유지가 필수다.
생산 키와 테스트 키는 별도 정책이다.
fixture-profile은 공개 키만 저장한다. CI는 개인 키를 저장하지 않는다.

UAC 거부는 Win32 1223으로 기록한다.
설치 도우미 종료 코드 0만으로 완료를 표시하지 않는다.
하네스는 보호된 outcome과 설치 버전을 함께 확인한다.
Committed와 Restored는 StageName으로 확인한다.
outcome이 없거나 시간 제한이 끝나면 PARTIAL을 유지한다.
capture는 설치 파일 해시, 보호 경로 owner와 SDDL을 기록한다.
acl-write-probe는 별도 일반 사용자 세션의 쓰기 결과를 기록한다.

## 호스트에서 확인한 결과

marker 없는 호스트 호출은 다음 출력으로 중단됐다.
`BLOCKED: disposable VM marker missing.`
종료 코드: 1.
호스트 인증서 저장소, 실제 설치본과 실제 금고는 변경하지 않았다.
호스트의 실제 관리자 토큰은 False였다.
합성 프로세스 시험은 NTFS에서 실행했다. 보호 ProgramData ACL 시험을 대체하지 않는다.

## VM 실행 절차

1. 폐기 가능한 Windows 11 VM과 별도 시험 사용자를 준비한다.
2. 초기 스냅샷 ID를 기록한다.
3. VM 전용 marker를 만든다.
4. prepare를 실행하여 VM 전용 공개 신뢰 프로파일을 만든다.
5. WO05VMTest=true와 WO05TestTrustSource로 Release를 빌드한다.
6. 앱의 중요 EXE, DLL과 도우미 런타임을 VM 인증서로 서명한다.
7. 검토된 설치기로 Program Files/PC-Care-WO05-Lab에 설치한다.
8. 합성 설정과 Lab 금고를 만든다.
9. capture로 버전, 파일 해시와 ACL을 저장한다.
10. VM 전용 ECDSA 키로 v2 업데이트 패키지를 만든다.
11. 일반 사용자 세션에서 apply와 UAC 승인을 시험한다.
12. outcome, 버전, 파일 해시와 금고 왕복을 대조한다.
13. 초기 스냅샷으로 복원한다.
14. UAC 거부, 이미지 잠금, DLL 잠금과 도우미 손상을 각각 시험한다.
15. 각 시나리오 이후 초기 스냅샷으로 복원한다.
16. 적용 중 종료, 복원 중 종료와 Windows 재부팅을 각각 시험한다.
17. acl-write-probe를 별도 일반 사용자로 실행한다.
18. GUI 주요 메뉴와 설정 저장을 직접 확인한다.
19. 각 결과의 명령, 스냅샷 ID와 실제 출력을 저장한다.
20. 시험이 끝나면 VM과 시험 개인 키를 폐기한다.

스냅샷 복원, UAC 선택과 재부팅은 수동 절차다.
하네스는 하이퍼바이저 스냅샷을 자동 복원하지 않는다.
전용 VM 없는 상태에서 실제 사용자 PC에 설치 시험을 대신 실행하지 않는다.

## 검증 및 복원

실제 명령, 입력, 출력, 종료 코드는 `evidence/wo06/VERIFICATION.txt`에 기록했다.
개별 프로세스 121개 명령은 `evidence/wo06/PROCESS_COMMANDS.json`에 기록했다.
소스 복원은 표시된 독립 복사본에 `SourceRollback.ps1` 또는 `ROLLBACK.sh`를 실행한다.
설치 복원은 인증된 부트스트랩의 `--recover`를 실행한다.
소스 해시 복원과 설치 트랜잭션 복원은 별도 결과다.
실제 사용자 설치본과 금고는 시험에 사용하지 않았다.
