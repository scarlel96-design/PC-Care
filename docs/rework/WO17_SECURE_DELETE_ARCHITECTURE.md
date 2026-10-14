# WO-17 — 보안 삭제 호출 구조

상태: PARTIAL. Phase 1 출시 판정: NO-GO.

작업 브랜치: `codex/pccare-wo17-safe-storagecare-secure-delete`.
기준 HEAD: `229ede0c0488c2e878f212a4d6420bfb10c44a36`. 기준 트리: `3ab0390bf592ff7035511c0891084a4b6e639e63`.
검증 소스: `63febc9ca94d8da90e5bfb3696f85f4ccc0ec125`. 소스 트리: `4d14aecd85a0962cd8664ea4bf8a1ec6e2dfdaba`.
Windows 11 Pro 26200, .NET SDK 10.0.401, 논리 CPU 24개에서 실행했습니다.
최종 증거 커밋과 CI 결과는 `evidence/wo17/GITHUB_RESULT.txt`에서 확인하십시오.

`SecureDeleteCenterPage` → `ProfessionalSecureDeleteService` → `ForensicSecureDeleteEngine` 경로가 실제 연결돼 있습니다.
기존 보안 삭제는 실행 기능입니다. StorageCare Dry Run과 구분했습니다.
선택 → 경로 정책 → 사전 검사 → 내용 포함 스냅샷 → 동의 → 재검사 → 핸들 확보 순서입니다.
최종 파일은 `WindowsFileBoundary.OpenWrite(deletable:true)`로 엽니다.
같은 핸들에서 내용 해시와 신원을 확인합니다. 같은 핸들로 덮어쓰기·표본 검증·삭제합니다.
경로 기반 이름 변경, 자동 읽기 전용 해제, ADS 제거와 볼륨 ReTrim 경로를 제거했습니다.
ADS 대상은 검토 차단합니다. ADS 열거 오류도 성공으로 처리하지 않습니다.
폴더 전체 실행은 마지막 디렉터리 삭제 경계가 미검증이므로 차단합니다.
개별 파일 실행과 폴더의 읽기 전용 계획은 유지합니다.
