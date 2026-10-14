# WO-17 — 저장 공간 검사

상태: PARTIAL. Phase 1 출시 판정: NO-GO.

작업 브랜치: `codex/pccare-wo17-safe-storagecare-secure-delete`.
기준 HEAD: `229ede0c0488c2e878f212a4d6420bfb10c44a36`. 기준 트리: `3ab0390bf592ff7035511c0891084a4b6e639e63`.
검증 소스: `63febc9ca94d8da90e5bfb3696f85f4ccc0ec125`. 소스 트리: `4d14aecd85a0962cd8664ea4bf8a1ec6e2dfdaba`.
Windows 11 Pro 26200, .NET SDK 10.0.401, 논리 CPU 24개에서 실행했습니다.
최종 증거 커밋과 CI 결과는 `evidence/wo17/GITHUB_RESULT.txt`에서 확인하십시오.

`StorageInventory`를 기존 화면과 연결했습니다. 기본 사용자 임시 폴더 검사 깊이는 2입니다.
항목은 512개, 시간은 5초로 제한합니다. 깊이 상한은 4입니다.
기존 `CaptureIsolated` 기본 깊이 0 계약을 유지했습니다. 별도 정책으로 하위 검사를 시험했습니다.
파일 ID, 볼륨 ID, 크기, 수정 시각, 소유자를 열린 핸들에서 확인합니다.
`WindowsFileBoundary`는 상위 폴더를 고정하고 재분석 지점과 하드링크를 거부합니다.
예산 초과, 접근 제한, 잠금과 깊이 제한을 누락 이유로 반환합니다.
누락 용량은 추정하지 않습니다. 디렉터리 항목도 예산을 사용합니다.
검사 메모리는 512개 결과와 제한된 대기 목록으로 제한합니다.
근거: `STORAGE_INVENTORY_RESULT.json`, 신규 39개 회귀 시험.
남은 범위: 사용자별 캐시 공급자 허용 목록, 장시간 반복 검사, 실제 저사양 장치.

시간 예산은 항목 경계에서 확인합니다. 단일 Windows API 호출 지연은 예산을 초과할 수 있습니다.
