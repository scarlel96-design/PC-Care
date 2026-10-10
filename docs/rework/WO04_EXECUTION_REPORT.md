# WO-04 실행 보고서

작업 브랜치: `codex/pccare-rework-phase1-final-validation`  
기준 커밋: `14b1a8151a742db577e92a586969a18625c06270`  
최종 검증 소스 커밋·트리·CI 실행 번호: `evidence/wo04/SOURCE_IDENTITY.txt` 및 `CI_RESULT.txt` 참조. 증거 갱신 커밋은 검증 소스 커밋과 구분한다.  
실행 명령·입력·출력·종료 코드는 `evidence/wo04/VERIFICATION.txt`, `TEST_RESULT.txt`, `RECOVERY_TEST_RESULT.txt`, `BUILD_RESULT.txt`에 보존한다.


상태: PARTIAL. Phase 1 출시 판정: NO-GO.

## 확인한 결함과 실제 수정

- 첫 파일 교체 뒤 적용 개수 증가가 optional callback 안에서만 실행되어 콜백이 없는 생산 호출에서 FilesApplied=0이 됐다. 증가를 독립시켜 첫 교체 장애 주입과 결과 기록을 바로잡았다.
- 새 트랜잭션의 앱 시작 연결이 없었다. 읽기 전용 InspectRecovery, 시작 로그, 업데이트 화면의 관리자 복구 실행을 연결했다. 일반 앱은 설치 파일을 복사하지 않는다.
- 복구 중 저널의 트랜잭션 ID·설치 루트·경로·중복·해시·JSON 크기를 검사한다. 백업 전체 사전 검증 후에만 복원을 시작한다. 손상된 백업 때문에 다른 새 파일을 먼저 삭제하던 부분을 보완했다.
- 복원 임시 파일 이름을 고유하게 만들어 복원 복사 중 중단 후 재시도 충돌을 줄였다.
- 기존 ProgramData ACL은 일반 앱이 읽지도 못하여 정상 완료 뒤에도 복구 필요로 오인할 수 있었다. 검증된 관리자 helper 경로에서 Users 읽기/실행 권한만 부여하도록 했다. 쓰기·삭제·ACL 변경은 계속 Admin/System에 제한한다. 실측은 별도 BLOCKED다.
- helper의 --apply 경로가 앱 종료를 기다리기 전에 Recover를 호출하던 순서를 수정했다. --recover에도 --wait-pid를 연결했다.
- 복구 필요 상태에서 새 업데이트를 차단하고 UAC 취소·helper 누락·진행 중·복구 완료 안내를 구분한다. 자동 적용 문구를 발행자 검증과 관리자 승인 흐름에 맞췄다.
- 무서명 런타임의 PublisherTrust를 Unknown으로 유지한다. Release 보안 게이트를 완화하지 않았다.

## 로컬과 원격 증거 정합성

로컬 Git 객체에서 9ba71ebb769c782508fb102df72fd7e8babdcee0과 14b1a8151a742db577e92a586969a18625c06270의 트리는 모두 5c104a5b0fff21e7fff75aa226d3e289504e0f77이고 파일 diff는 비어 있다. 부모는 각각 3da30573fb6a7a301a8069f2c95295cf398b23d1과 71b54710b232a637143413840cc7993f57e1428f다. 커밋 역사는 다르고 파일 내용은 같다. 원격 객체를 fetch한 뒤 새 작업 브랜치를 지정된 원격 커밋으로 맞췄다. 기존 브랜치 역사와 수정 파일을 보존했다. 강제 원격 푸시는 사용하지 않는다.

WO03 VERIFICATION의 FINAL SHA와 CI 기록을 14b1a81 / run 38024249499 / 공유 트리 5c104a5로 수정했다. 이 이전 CI를 WO04 통과로 사용하지 않는다.

## GitHub만으로 보이지 않는 실행 환경

로컬 경로는 C:/Users/scarl/Documents/Codex/2026-07-18/c-users-scarl-desktop-pccare-pc/work/PC-Care다. 기준선은 옆의 PC-Care-WO04-baseline 독립 체크아웃이다. SDK 10.0.401, host runtime 10.0.12, win-x64에서 실행했다. 앱 버전은 계속 51.0.4이며 배포 버전·설치본 교체를 수행하지 않았다.

제한된 실행에서 VSTest 연결이 90초/300초 시간 초과했고 Git fetch getaddrinfo 오류와 Git Bash named-object 접근 오류가 발생했다. 승인된 제한 외 실행에서 Git 인증·fetch, 기준선 테스트와 Bash 복원을 실제 재실행했다. 이전 실패 기록은 최종 성공과 구분했다. 기준선 최초 test/build는 별도 worker assets 누락 NETSDK1004로 실패했고, 이미 빌드된 기준선 assembly에 --no-build 테스트를 실행하여 결과를 얻었다.

솔루션 build의 테스트 출력은 bin/Release이고 -p:Platform=x64 테스트 출력은 bin/x64/Release였다. 초기 수정본 실행은 38개 중 1개 실패한 오래된 바이너리였다. 테스트 프로젝트를 x64로 직접 build하고 CI에서도 해당 테스트 step에 build를 포함해 최신 소스를 검사하도록 했다.

폐기 가능한 VM, 복구 스냅샷, vmrun/VBoxManage/qemu/Get-VM 명령은 제공되지 않았다. 사용자 실금고/설치본은 사용하지 않았다. ProgramData/PC-Care/UpdateTransactions는 검사 당시 존재하지 않았으며 생성하지 않았다. UAC 승인·거부, 실제 Windows 재부팅, 생산 서명 패키지는 실행 증거가 없다.

## 변경 파일 및 복원

변경 파일 목록은 evidence/wo04/CHANGED_FILES.txt, 코드 diff는 DIFF_FILE.patch에 있다. 전체 소스 복원은 독립 브랜치에서 검증 소스 커밋을 git revert한다. ROLLBACK.sh는 독립 UpdateTransaction.cs 복사본만 기준 해시로 복원한다. 설치 복원은 고정 설치 위치의 관리자 UpdateHelper --recover 경로이며 소스 복원과 다른 검증이다.

## 다음 GPT에 전달할 요청

```text
PC-Care WO04_EXECUTION_REPORT, RELEASE_READINESS, SOURCE_IDENTITY, CI_RESULT와 7개 보고서를 함께 검토하라. 자동 합성 테스트 PASS, 별도 프로세스 FailFast 복원 PASS, 실제 VM/UAC/재부팅 BLOCKED, 생산 키 BLOCKED를 구분하라. 남은 P0는 파일/디렉터리 핸들 고정과 식별자 재검증, 실제 보호 ACL 실측, 테스트 전용 Release VM 빌드의 신뢰 루트 주입, 16개 설치 E2E 시나리오다. Release 무서명 게이트를 풀지 말고 main 병합/공식 릴리스 없이 다음 독립 브랜치에서 개선하라.
```
