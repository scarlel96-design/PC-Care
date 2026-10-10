# WO-06 출시 판정

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

Status: PARTIAL. Phase 1 공식 출시: NO-GO.

| 항목 | 판정 | 근거 |
|---|---|---|
| 별도 부트스트랩 코드 | PARTIAL | 구현과 악성 외부 경로 거부. 인증된 실제 실행 미검증 |
| 이미지와 DLL 잠금 회귀 | PASS | Windows 합성 프로세스, 복구 후 재적용 |
| Schema 1 완료 이관 | PASS | 원본 해시 보존과 후속 Schema 2 적용 |
| Schema 1 미완료 복구 | PARTIAL | 보존과 차단. 상세 진단 구현. 실제 관리자 복구 미검증 |
| 저널 프로세스 중단 | PASS | 별도 프로세스 복원과 이전 세대 검사 |
| 정전과 Windows 재부팅 | BLOCKED | 전용 VM 미제공 |
| 신규 파일 미기록 ID | PARTIAL | 임시 이름 보존. 자동 복구 창 잔존 |
| Authenticode 동일성 | PARTIAL | 실제 내장 서명 시험. 공식 발행자 검증 미제공 |
| 금고와 설정 회귀 | PASS | 합성 암호화 금고 API 왕복과 설정 보존 |
| 대화형 회수 CLI | PASS | Lab v5 한 파일 내보내기와 원본 해시 보존 |
| Lab v4 대화형 CLI | NOT TESTED | 이번 작업의 새 실행 증거 없음 |
| 보호 ACL 실측 | BLOCKED | 일반 사용자 및 관리자 VM 양쪽 시험 미실행 |
| 공식 서명 패키지 | BLOCKED | 생산 발행자 인프라 없음 |
| 실제 앱 GUI 및 설치 E2E | BLOCKED | 전용 VM 없음 |

## 남은 P0

1. 인증된 부트스트랩 자체 갱신과 이전 도우미 복원 E2E를 실행해야 한다.
2. WO-05 구형 도우미의 최초 전환 설치기를 검토하고 검증해야 한다.
3. 신규 임시 파일의 ID 미기록 창을 처리하는 출처 증명 프로토콜이 필요하다.
4. 구형 미완료 저널의 실제 관리자 복구를 검증해야 한다.
5. 실제 보호 ACL, 재부팅, UAC와 실패 복원 증거가 필요하다.
6. 공식 공개 신뢰 루트, 개인 키 관리와 서명 패키지가 필요하다.

자동 테스트 통과를 생산 설치 검증 통과로 승격하지 않았다.
공식 생산 핀이 없으므로 Release의 보호 기능은 기존 엄격한 게이트를 유지한다.
기존 사용자 기능의 출시 호환성은 공식 서명 전환 정책이 결정해야 한다.
위 조건이 끝날 때까지 main 병합과 공식 릴리스는 진행하지 않는다.

후속 ETW 진단과 성능 측정 설계는 독립 브랜치에서 진행할 수 있다.
성능 복구 제품 개발은 이 업데이트 엔진의 출시 승인과 별도다.
이 작업에서는 성능 개선과 UI 전면 개편을 주장하지 않는다.

## 검증 및 복원

실제 명령, 입력, 출력, 종료 코드는 `evidence/wo06/VERIFICATION.txt`에 기록했다.
개별 프로세스 121개 명령은 `evidence/wo06/PROCESS_COMMANDS.json`에 기록했다.
소스 복원은 표시된 독립 복사본에 `SourceRollback.ps1` 또는 `ROLLBACK.sh`를 실행한다.
설치 복원은 인증된 부트스트랩의 `--recover`를 실행한다.
소스 해시 복원과 설치 트랜잭션 복원은 별도 결과다.
실제 사용자 설치본과 금고는 시험에 사용하지 않았다.

## 최종 Windows CI 결과

CI 38054469100은 소스 5647b352cf41082def74bef2701b6fe707c51cc0에서 success로 완료됐다.
원격 TRX를 내려받아 128개 관련 시험, 전체 769 PASS / 111 SKIP, SecurityLab 36 PASS를 확인했다.
121개 프로세스 명령은 81개 정상 종료, 37개 의도한 강제 종료, 3개 예상 차단 결과를 확인했다.
Release 빌드는 경고 0개와 오류 0개를 기록했다.
실제 VM 설치와 생산 서명 항목은 BLOCKED 상태를 유지한다.
기존 복구 버튼은 도우미 실행 성공 후 ExitForUpdateHandoff를 호출한다.
이 앱 종료 연결은 코드로 확인했다. 실제 GUI 실행 증거는 없다.
