# WO-17 — 용량 계산

상태: PARTIAL. Phase 1 출시 판정: NO-GO.

작업 브랜치: `codex/pccare-wo17-safe-storagecare-secure-delete`.
기준 HEAD: `229ede0c0488c2e878f212a4d6420bfb10c44a36`. 기준 트리: `3ab0390bf592ff7035511c0891084a4b6e639e63`.
검증 소스: `63febc9ca94d8da90e5bfb3696f85f4ccc0ec125`. 소스 트리: `4d14aecd85a0962cd8664ea4bf8a1ec6e2dfdaba`.
Windows 11 Pro 26200, .NET SDK 10.0.401, 논리 CPU 24개에서 실행했습니다.
최종 증거 커밋과 CI 결과는 `evidence/wo17/GITHUB_RESULT.txt`에서 확인하십시오.

논리 크기와 Windows가 보고한 할당 크기를 분리했습니다.
`GetFileInformationByHandleEx(FileStandardInfo)`의 `AllocationSize`를 읽습니다.
조회 실패는 null입니다. 논리 크기로 대체하지 않습니다.
희소·압축·EFS·오프라인 파일은 자동 후보가 아닙니다.
하드링크는 처리하지 않습니다. 같은 파일 ID의 중복 선택도 차단합니다.
`StorageSpaceAccounting`은 후보, 격리, 복원, 최종 삭제와 볼륨 변화를 분리합니다.
동일 볼륨 격리의 확보 공간은 0입니다.
최종 삭제 후 실제 기여량은 null입니다. 외부 볼륨 변화는 별도 값입니다.
근거: `STORAGE_ACCOUNTING_RESULT.json`, `PERFORMANCE_BEFORE_AFTER_RESULT.json`.
실제 희소·압축 파일의 물리 할당 비교는 NOT TESTED입니다.
Microsoft 계약: https://learn.microsoft.com/en-us/windows/win32/api/winbase/ns-winbase-file_standard_info
