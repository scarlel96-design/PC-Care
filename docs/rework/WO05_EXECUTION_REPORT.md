# WO-05 실행 보고서

작업 브랜치: `codex/pccare-rework-phase1-filesystem-hardening`
기준 커밋: `155537b721e3eb266b2a0a3fb801a4b35fbbff56`
기준 트리: `581bdaf0bf50a787ea1ca346140065c733e56b12`
검증 소스 커밋·원격 CI: `evidence/wo05/GITHUB_RESULT.txt`.
최종 증거 커밋은 검증 소스 커밋과 구분한다.
실행 위치: `C:\Users\scarl\Documents\Codex\2026-07-18\c-users-scarl-desktop-pccare-pc\work\PC-Care`.
환경: Windows NT 10.0.26200.0, x64, .NET SDK 10.0.401, runtime 10.0.12.
전용 VM 및 스냅샷: 미제공.

전체 판정: PARTIAL. Phase 1 출시 판정: NO-GO.

## 실제 변경

- WindowsFileBoundary는 디렉터리 조상 핸들과 파일 핸들을 유지한다.
- FileIdInfo는 볼륨 식별자와 128비트 파일 식별자를 제공한다.
- 패키지 검증과 복사는 동일한 원본 핸들을 사용한다.
- 스테이징 검증과 설치 쓰기는 유지된 핸들을 사용한다.
- 모든 백업과 설치 대상 핸들을 준비한 뒤 기존 내용을 변경한다.
- 기존 대상은 제자리 쓰기로 식별자를 유지한다.
- 저널 schema 2는 설치·트랜잭션·원본·백업·설치 파일 식별자를 저장한다.
- 복구는 모든 백업과 대상을 먼저 검증한다.
- 이중 장애 결과는 원래 오류와 추가 저널 오류를 함께 보존한다.
- 보호 모드는 파일 핸들에서 ACL을 읽고 하위 디렉터리 ACL도 확인한다.
- 성공한 트랜잭션은 검증된 내부 패키지를 핸들로 삭제한다.
- 실패한 트랜잭션은 패키지·백업·저널을 보존한다.
- VM 전용 Release 프로파일은 생산 프로파일과 컴파일 단계에서 분리된다.
- 독립 금고 회수 도구는 원본을 읽기 전용으로 유지하고 복사본에서 내보낸다.

변경 파일 및 SHA-256: `evidence/wo05/SOURCE_MANIFEST.json`.
변경 패치: `evidence/wo05/SOURCE_CHANGES.patch`.

## 자동 검증

| 검사 | 실제 결과 | 종료 코드 |
|---|---|---|
| 변경 전 SignedUpdateTests | 53 PASS / 0 FAIL / 0 SKIP | 0 |
| 최종 관련 테스트 | 91 PASS / 0 FAIL / 0 SKIP | 0 |
| 전체 앱 | 732 PASS / 111 SKIP / 0 FAIL / 843 total | 0 |
| SecurityLab | 36 PASS / 0 FAIL / 0 SKIP | 0 |
| Release 솔루션 빌드 | 경고 0 / 오류 0 | 0 |
| 적용 강제 종료 후 별도 복구 | 원본 old / vault-marker / RecoveryCompleted | 0 |
| 복원도 강제 종료한 뒤 재실행 | 원본 old / vault-marker / RecoveryCompleted | 0 |
| VM Release 앱 프로파일 컴파일 | 경고 0 / 오류 0 | 0 |
| VM 아티팩트 생산 감사 | 앱·엔진 거부 | 6, 예상된 거부 |
| 일반 Release 생산 감사 | 앱·엔진·도우미 시험 루트 없음 | 0 |

새 테스트 38건을 추가했다. 기존 111 SKIP은 유지했다.
기존 하드링크·백업 삭제 시험은 강화된 거부·복원 결과를 검사하도록 수정했다.
중요 보안 시험을 삭제하거나 SKIP으로 변경하지 않았다.

## GitHub에서 알 수 없는 로컬 정보

실제 사용자 설치본, 금고, ProgramData, Program Files는 변경하지 않았다.
시험 데이터는 임시 합성 폴더와 저장소 artifacts 하위에 배치했다.
실제 VM·스냅샷·UAC 승인·거부·재부팅 로그는 없다.
호스트 CIM 조회는 실행 환경의 접근 제어 때문에 실패했다.
시험 서명 키의 개인 부분은 로컬 fixture-profile 실행에서 저장하지 않았다.
시험 Authenticode 인증서 생성·신뢰 등록은 호스트에서 실행하지 않았다.
회수 도구의 API는 검사했다. 대화형 비밀번호 입력 CLI는 NOT TESTED다.
소스 복원은 artifacts의 별도 복사본에서 수행했다. 설치 트랜잭션 복원과 구분한다.
원래 소스 SHA-256: `3B3E198CE830BA48AB0F11F31F0CCF9F0ACCB44E0D47F8A6527CEF2298532067`.

초기 소스 복원은 Windows PowerShell의 Get-FileHash 탐색 실패로 종료 코드 1을 반환했다.
스크립트는 .NET SHA-256을 사용하도록 수정했다. 재실행은 종료 코드 0을 반환했다.
초기 백업 삭제 시험은 기존 기대값과 강화된 잠금 결과가 달랐다.
시험은 삭제 시도의 거부와 원본 복원 여부를 함께 검사하도록 수정했다.

## 남은 위험

| 항목 | 상태 | 필요한 후속 증거 |
|---|---|---|
| 실행 중인 도우미·DLL 자체 업데이트 | PARTIAL / P0 | 유지 중인 이미지 잠금 때문에 쓰기 열기가 실패한다. 신뢰된 별도 실행 위치 설계와 설치본 E2E가 필요하다. |
| 실제 보호 ACL | BLOCKED / P0 | VM에서 소유자·DACL·하위 파일·Users 거부·SYSTEM 접근·재부팅 후 유지 여부를 측정한다. |
| 관리자 공격·ACL 변경·기존 쓰기 메모리 매핑 | NOT TESTED / P0 | 보호 ACL을 수정할 수 있는 주체는 현재 신뢰 경계 내부다. 파일 ID는 암호학적 인증 수단이 아니다. |
| 저널 원자 교체와 전원 손실 | PARTIAL / P0 | 임시 파일 핸들을 닫은 뒤 File.Move를 실행한다. 보호 부모 ACL에 의존한다. 교체 순간 강제 종료와 전원 손실은 추가 시험이 필요하다. |
| 파일 생성 후 ID 저널 기록 전 중단 | PARTIAL / P0 | 빈 새 파일이 남을 수 있다. ID가 없는 파일은 복구에서 삭제하지 않는다. 실패 증거를 보존한다. |
| WO04 구형 저널 | PARTIAL / P0 | 식별자가 없는 구형 저널은 완료 상태도 거부한다. 검토된 이관·보관 절차가 필요하다. |
| 실제 설치·UAC·Windows 재부팅 | BLOCKED / P0 | 폐기 가능한 VM, 스냅샷, 승인·거부·재부팅 로그가 필요하다. |
| 공식 발행자·생산 패키지 | BLOCKED / P0 | 승인된 신뢰 루트·개인 키 운영·회전 정책·생산 서명 패키지가 필요하다. |
| AV3 데이터 회수·구형 실제 설치본 | NOT TESTED | 회수 도구는 Lab v4/v5만 지원한다. AV3 Writer와 마이그레이션 게이트를 유지한다. |


## 다음 작업용 입력

다음 프롬프트를 재사용한다.

```text
WO05_RELEASE_READINESS.md와 evidence/wo05/GITHUB_RESULT.txt를 읽어라.
검증 소스 SHA와 실제 작업 트리를 대조하라.
잠긴 도우미 자체 업데이트와 구형 저널 이관을 우선 해결하라.
시험은 폐기 가능한 Windows VM과 합성 금고에서 수행하라.
UAC·재부팅·보호 ACL·공식 서명을 자동 시험과 구분하라.
AV3 Writer와 마이그레이션 게이트를 유지하라.
단위 테스트 PASS만으로 출시를 승인하지 말라.
별도 제품 브랜치에서 ETW 원인 진단과 전후 성능 측정을 진행하라.
main 병합과 공식 릴리스는 승인 전 수행하지 말라.
```
