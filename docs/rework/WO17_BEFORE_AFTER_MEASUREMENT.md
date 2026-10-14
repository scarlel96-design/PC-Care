# WO-17 — 전후 측정

상태: PARTIAL. Phase 1 출시 판정: NO-GO.

작업 브랜치: `codex/pccare-wo17-safe-storagecare-secure-delete`.
기준 HEAD: `229ede0c0488c2e878f212a4d6420bfb10c44a36`. 기준 트리: `3ab0390bf592ff7035511c0891084a4b6e639e63`.
검증 소스: `63febc9ca94d8da90e5bfb3696f85f4ccc0ec125`. 소스 트리: `4d14aecd85a0962cd8664ea4bf8a1ec6e2dfdaba`.
Windows 11 Pro 26200, .NET SDK 10.0.401, 논리 CPU 24개에서 실행했습니다.
최종 증거 커밋과 CI 결과는 `evidence/wo17/GITHUB_RESULT.txt`에서 확인하십시오.

1MiB 합성 파일을 같은 볼륨에서 격리하고 복원했습니다.
원본과 복원본의 전체 SHA-256을 비교했습니다.
별도 65,537바이트 합성 파일을 보안 삭제했습니다.
실제 삭제 부재와 감사 체인 검증을 확인했습니다.
검사 시간·CPU 시간·작업 집합과 볼륨 여유 공간은 JSON에 기록했습니다.
겉으로 관측한 볼륨 변화는 작업 기여량으로 환산하지 않았습니다.
동일 볼륨 격리의 기여량은 0입니다.
작업 완료 속도와 Windows 성능 개선율은 null입니다.
단일 장비의 단일 실행입니다. 저사양 실물 반복 측정은 NOT TESTED입니다.

로컬 전후 측정 중 기존 전체 회귀도 실행 중이었습니다. 전역 볼륨 변화는 해당 배경 작업의 영향을 포함합니다.
