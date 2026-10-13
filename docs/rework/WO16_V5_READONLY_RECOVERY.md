# WO16 V5 READONLY RECOVERY

작업 브랜치: `codex/pccare-wo16-standard-mode-stability`.
기준 HEAD: `60fc9a6c812e5458d5367020b732ca0c199ea72e`. 기준 트리: `57d21a8fd06231145897e09bfdc09f9c45764e01`.
검증 소스: `1ca80b98f63ec953c952647fcf28f8032ac986a7`. 검증 소스 트리: `61dc70b16d6d359d562cc26f6fbf65c2e3ef2803`.
판정: **PARTIAL / Phase 1 NO-GO**.
명령·입력·실제 출력·종료 코드는 `evidence/wo16/`에 기록했다.
최종 증거 커밋과 원격 HEAD는 최종 제출 기록에서 확인한다.

## 실패 상태와 회수 조건
LegacyPasswordHeadersWritten 및 LegacyPasswordSlotsWritten을 별도 프로세스에서 재현했다.
두 상태 모두 기존·신규 비밀번호의 정상 잠금 해제가 실패했다.
이 결과를 정상 Unlock 성공으로 재분류하지 않았다.
독립 보관한 이전 인증 헤더를 별도 후보 사본에만 복원했다.
정상 UnlockReadOnly가 VMK 래핑·헤더 MAC·메타데이터 인증·커밋 바인딩을 검증했다.
복구 슬롯은 덮어쓰지 않았다. 원본 금고의 전체 파일 해시와 목록을 보존했다.
추출은 새 출력 위치로 수행했다. 결과는 ReadOnlyRecovery이며 전환 완료가 아니다.
두 합성 파일의 원본·추출 SHA-256은 다음과 같다.
`74F81FE167D99B4CB41D6D0CCDA82278CAEE9F3E2F25D5E5A3936FF3DCEC60D0`.
잘못된 헤더 MAC은 회수 실패로 차단했다.
이전 인증 헤더·정당한 비밀번호가 없으면 회수를 보장하지 않는다.
이미 덮어써진 메타데이터와 암호학적 인증이 불가능한 객체는 복원 성공으로 표시하지 않는다.
공개 v4/v5 비밀번호 변경은 계속 차단한다. 생산 자동 전환은 비활성화다.
