# WO-17 — 결과 안내와 사용 흐름

상태: PARTIAL. Phase 1 출시 판정: NO-GO.

작업 브랜치: `codex/pccare-wo17-safe-storagecare-secure-delete`.
기준 HEAD: `229ede0c0488c2e878f212a4d6420bfb10c44a36`. 기준 트리: `3ab0390bf592ff7035511c0891084a4b6e639e63`.
검증 소스: `63febc9ca94d8da90e5bfb3696f85f4ccc0ec125`. 소스 트리: `4d14aecd85a0962cd8664ea4bf8a1ec6e2dfdaba`.
Windows 11 Pro 26200, .NET SDK 10.0.401, 논리 CPU 24개에서 실행했습니다.
최종 증거 커밋과 CI 결과는 `evidence/wo17/GITHUB_RESULT.txt`에서 확인하십시오.

기존 WinUI 3 디자인 토큰과 목록을 재사용했습니다.
StorageCare는 논리 크기·할당 크기·누락·격리·복원·삭제와 미측정을 분리합니다.
검사는 Task.Run에서 수행합니다. 결과 목록은 최대 512개입니다.
보안 삭제 화면은 완료, 일부 완료, 취소와 실패를 한국어로 표시합니다.
폴더 전체 실행은 비활성화하며 개별 파일 재검사를 안내합니다.
삭제 저항 Level 숫자를 인증 수준처럼 표시하는 문구를 제거했습니다.
취소·실패 시 부분 덮어쓰기와 원복 제한을 설명합니다.
고 DPI, 축소 모션, 키보드·스크린리더와 UI 반응성은 실측하지 않았습니다.

남은 UI P1: 미처리 항목 선택 유지와 항목별 결과 연결을 실제 GUI에서 검증하십시오.
Dry Run 전체 해시의 UI 스레드 비용을 측정하십시오.
고 DPI, 키보드와 스크린리더 검증은 NOT TESTED입니다.
