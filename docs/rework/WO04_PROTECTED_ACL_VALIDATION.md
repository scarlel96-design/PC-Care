# WO-04 보호 ACL 및 경로 검증

작업 브랜치: `codex/pccare-rework-phase1-final-validation`  
기준 커밋: `14b1a8151a742db577e92a586969a18625c06270`  
최종 검증 소스 커밋·트리·CI 실행 번호: `evidence/wo04/SOURCE_IDENTITY.txt` 및 `CI_RESULT.txt` 참조. 증거 갱신 커밋은 검증 소스 커밋과 구분한다.  
실행 명령·입력·출력·종료 코드는 `evidence/wo04/VERIFICATION.txt`, `TEST_RESULT.txt`, `RECOVERY_TEST_RESULT.txt`, `BUILD_RESULT.txt`에 보존한다.


상태: PARTIAL. 보호 ProgramData ACL 실제 측정은 BLOCKED.

관리자 helper의 strict 경로는 ProgramData/PC-Care/UpdateTransactions만 허용한다. 소유자는 Administrators/System, DACL 상속 보호, 다른 주체의 WriteData/AppendData/WriteAttributes/WriteExtendedAttributes/Delete/DeleteSubdirectoriesAndFiles/ChangePermissions/TakeOwnership 허용을 거부한다. 기존 검사가 FileSystemRights.Modify 복합 비트를 쓰면 읽기 권한도 쓰기로 오인하므로 개별 변경 권한 비트로 수정했다. explicit·inherited ACE를 모두 검사한다. Users 읽기/실행만 새 루트 및 검증된 이전 루트에 부여하여 비관리자 시작 검사를 지원한다. 검증되지 않은 owner/DACL은 수정하여 정상화하지 않고 중단한다.

측정 당시 보호 루트는 존재하지 않았다. owner/실제 ACL/일반 사용자 쓰기 거부/관리자 쓰기/하위 상속/재부팅 유지/기존 잘못된 owner와 ACL은 NOT TESTED 또는 VM 부재 BLOCKED다. 정적 코드 검사로 ACL 완료 판정을 내리지 않는다. scripts/wo04-vm-evidence.ps1은 VM marker와 snapshot ID를 요구하고 전후 설치 해시·owner/SDDL을 수집한다.

| 시나리오 | 증거와 판정 |
|---|---|
| 하드링크 외부 파일 | 기존 자동 테스트: temp+move로 외부 old 내용 유지 |
| 경로 traversal·예약 이름·ADS·끝점 | 자동 부정 입력 테스트 |
| case 충돌 | 복구 저널 중복 부정 테스트; 패키지 allowlist |
| 손상 백업 | 전체 사전 검증 후 추가 변경 중지 테스트 |
| 스테이징 내용 | 추출 후·교체 직전·대상 temp에 크기와 SHA256 검사 |
| 동시 적용/복구 | FileShare.None lock 자동 테스트 |
| 정션·백업 교체의 실제 공격 | NOT TESTED |
| 긴 경로 경계값 | NOT TESTED |
| 검증과 파일 open 사이 경쟁 | 잔여 위험 PARTIAL |

파일 읽기는 FileStream FileShare.Read로 열며 쓰기/삭제 공유는 허용하지 않는다. temp는 CreateNew/WriteThrough/Flush(true), 교체는 File.Move overwrite다. 그러나 모든 부모 디렉터리 핸들을 고정하지 않으며 파일 ID/볼륨 ID를 비교하지 않는다. GuardExistingPath 이후 junction을 바꾸는 공격이나 journal/backup 검사 이후 경로 교체의 TOCTOU가 남는다. 다음 P0 작업은 부모 핸들 고정, reparse 없는 handle-relative 연산, 파일 식별자와 open 후 해시 검증, 실제 격리 경쟁 공격 실측이다. 이 위험을 제거했다고 주장하지 않는다.
