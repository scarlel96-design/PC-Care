# WO-17 — 정리 대상 정책

상태: PARTIAL. Phase 1 출시 판정: NO-GO.

작업 브랜치: `codex/pccare-wo17-safe-storagecare-secure-delete`.
기준 HEAD: `229ede0c0488c2e878f212a4d6420bfb10c44a36`. 기준 트리: `3ab0390bf592ff7035511c0891084a4b6e639e63`.
검증 소스: `63febc9ca94d8da90e5bfb3696f85f4ccc0ec125`. 소스 트리: `4d14aecd85a0962cd8664ea4bf8a1ec6e2dfdaba`.
Windows 11 Pro 26200, .NET SDK 10.0.401, 논리 CPU 24개에서 실행했습니다.
최종 증거 커밋과 CI 결과는 `evidence/wo17/GITHUB_RESULT.txt`에서 확인하십시오.

현재 사용자 소유이며 7일 이상 된 .tmp/.cache만 후보로 분류합니다.
후보는 실행 승인이 아닙니다. 생산 정리 실행은 false입니다.
숨김·시스템·읽기 전용·링크·특수 할당 파일은 보호 또는 검토 대상입니다.
금고, 객체, 세대, 앵커, 영수증, 저널, 거래와 백업의 상위 폴더도 검사합니다.
인증서·키·DPAPI·금고 확장자와 금고 파일 이름을 보호합니다.
이름 정책은 보수적입니다. 모든 사용자 지정 저장소를 자동 발견하는 정책은 아직 없습니다.
Dry Run은 선택 중복, 파일 ID, 루트 ID, 소유자, 크기와 수정 시각을 다시 확인합니다.
실제 처리 시험은 기존 `SandboxRecovery`의 고정 합성 계약만 사용했습니다.
범용 사용자 파일 격리 실행기는 이번 단계에서 완성되지 않았습니다.
