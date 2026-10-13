# WO16 GENERATION LIFECYCLE

작업 브랜치: `codex/pccare-wo16-standard-mode-stability`.
기준 HEAD: `60fc9a6c812e5458d5367020b732ca0c199ea72e`. 기준 트리: `57d21a8fd06231145897e09bfdc09f9c45764e01`.
검증 소스: `1ca80b98f63ec953c952647fcf28f8032ac986a7`. 검증 소스 트리: `61dc70b16d6d359d562cc26f6fbf65c2e3ef2803`.
판정: **PARTIAL / Phase 1 NO-GO**.
명령·입력·실제 출력·종료 코드는 `evidence/wo16/`에 기록했다.
최종 증거 커밋과 원격 HEAD는 최종 제출 기록에서 확인한다.

## 내부 시험 계약
Import와 PasswordChange는 변경 직전 앵커와 인증 세대를 대조한다.
신규 세대는 이전 세대 번호보다 정확히 1 증가해야 한다.
VaultId와 복구 자료 바인딩은 유지하고 세대 바인딩은 변경해야 한다.
확정된 금고와 앵커는 서로 다른 저장소다. 하나의 원자적 갱신으로 표시하지 않는다.
금고 확정 후 앵커 기록이 실패하면 AnchorCommitPending을 반환한다.
확정된 금고를 삭제하거나 이전 앵커에 맞춰 덮어쓰지 않는다.
새 인증 세대로 읽기 전용 재열기와 콘텐츠 검증을 수행한다.
Import와 PasswordChange의 앵커 확정 전·후 강제 종료 4개 시험을 통과했다.
확정 전 중단은 AuthenticButStale를 유지한다. 확정 후 중단은 FreshnessVerified다.
외부 상태 갱신의 자동 재조정과 독립 최신성 검증은 미완료다.
복구 코드 Rotation은 기존 v6 경로에서 차단돼 있다. 새 생산 경로를 추가하지 않았다.
