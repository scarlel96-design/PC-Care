# WO16 VAULT INTEGRITY

작업 브랜치: `codex/pccare-wo16-standard-mode-stability`.
기준 HEAD: `60fc9a6c812e5458d5367020b732ca0c199ea72e`. 기준 트리: `57d21a8fd06231145897e09bfdc09f9c45764e01`.
검증 소스: `1ca80b98f63ec953c952647fcf28f8032ac986a7`. 검증 소스 트리: `61dc70b16d6d359d562cc26f6fbf65c2e3ef2803`.
판정: **PARTIAL / Phase 1 NO-GO**.
명령·입력·실제 출력·종료 코드는 `evidence/wo16/`에 기록했다.
최종 증거 커밋과 원격 HEAD는 최종 제출 기록에서 확인한다.

## 보호와 회귀
사용자 원본 보존, 충돌 시 추출 중단, 청크 인증, 전체 SHA-256 검증을 재사용했다.
작업 중 취소 시험은 독립 사본 생성 전에 중단되고 원본 해시가 유지됐다.
누락·변조된 이전 슬롯 파일은 조정을 차단했다.
금고·앵커 이름과 .avpack/.dpapi 파일은 저장 공간 정리에서 Protected로 분류한다.
StorageInventory는 기존처럼 하위 디렉터리에 진입하지 않는다.
기존 업데이트·보안 삭제·AV3 엔진 소스는 변경하지 않았다.
전체 앱 1139 PASS / 111 SKIP / 0 FAIL, 신규 WO16 31 PASS다.
기존 111 Skip의 테스트 이름이 기준선과 동일하다. 사유는 기존 코드에 유지했다.
업데이트 128과 성능 233은 전체 앱에 포함된다. SecurityLab 36은 별도다.

## 추출 측정
0바이트·4KiB·4MiB·64MiB·256MiB를 별도 프로세스에서 추출했다.
Unlock, Export, Hashing 구간을 따로 측정했다. Import는 측정 프로세스에서 실행하지 않았다.
256MiB Export는 2269.3384 ms, 관리 할당 16003504 bytes를 기록했다.
최대 표본 Working Set은 116330496 bytes다.
이 Working Set에는 이전 Unlock의 메모리 잔존이 포함된다.
Export에는 인증과 출력 비용이 포함된다. 5ms 표본은 순간 최대치를 놓칠 수 있다.
해시는 모든 크기에서 일치했다. 저사양 실물 시험과 물리적 전원 차단은 NOT TESTED다.
기존 시험과 부하 조건이 달라 성능 개선률을 계산하지 않았다.
