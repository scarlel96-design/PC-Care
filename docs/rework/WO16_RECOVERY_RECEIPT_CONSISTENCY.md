# WO16 RECOVERY RECEIPT CONSISTENCY

작업 브랜치: `codex/pccare-wo16-standard-mode-stability`.
기준 HEAD: `60fc9a6c812e5458d5367020b732ca0c199ea72e`. 기준 트리: `57d21a8fd06231145897e09bfdc09f9c45764e01`.
검증 소스: `1ca80b98f63ec953c952647fcf28f8032ac986a7`. 검증 소스 트리: `61dc70b16d6d359d562cc26f6fbf65c2e3ef2803`.
판정: **PARTIAL / Phase 1 NO-GO**.
명령·입력·실제 출력·종료 코드는 `evidence/wo16/`에 기록했다.
최종 증거 커밋과 원격 HEAD는 최종 제출 기록에서 확인한다.

## 원인과 구현
WO-15는 코드 소비와 앵커 확정 사이에서 중단된 예약을 조회만 했다.
예약에 인증된 이전 세대와 복구 슬롯 해시를 기록했다.
조정은 이전 슬롯 파일의 MAC·해시와 현재 슬롯 파일의 MAC·해시를 검증한다.
지정한 코드 한 개만 Unused에서 Used로 바뀌었는지 확인한다.
다른 슬롯 변경, 잘못된 코드, 누락된 이전 자료는 검토 필요로 차단한다.
조정은 복구 코드를 다시 소비하지 않는다.
같은 OperationId의 확정·확인 영수증은 재조회한다.
미완료 예약이 있으면 다른 코드 예약과 정상 세대 변경을 차단한다.
잘못된 상태·날짜·코드 식별자·중복 ID·예약 바인딩은 손상 상태로 반환한다.

## 실제 시험
TrustedAfterConsumption 및 AnchorBeforeReplace:2를 별도 프로세스에서 강제 종료했다.
재열기·조정·Acknowledgement·중복 조회·파일 추출을 실행했다.
둘 다 ReceiptAcknowledged 상태와 원본 바이트를 확인했다.
TrustedBeforeConsumption은 InterruptedReview를 유지했다. 코드 파일은 변경되지 않았다.
조정 과정의 6개 추가 종료 지점을 검증했다. 전체 신규 경로는 9개다.
기존 WO-15 15개 종료 경로도 재실행했다.
영수증은 영속 상태를 증명한다. 호출자에게 성공이 정확히 한 번 전달됐다는 증거는 아니다.
이전 WO-15 예약에 ReservedEvidence가 없으면 자동 조정을 차단한다.
