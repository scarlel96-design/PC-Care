# 기능 실제성 매트릭스

| 기능 | 구현 근거 | 자동 검증 | 실제 운영/독립 검증 | 판정 |
|---|---|---|---|---|
| 시스템 케어 | `SystemCareService`와 규칙/결과 UI | 안정성·복원 충돌 테스트 존재 | 실제 PC 전후·오탐 측정 없음 | PARTIAL |
| 통합 점검 | 앱 서비스와 진행 UI | 일부 단위 테스트 | 창 크기/DPI/반복 사용 미확인 | PARTIAL |
| 보안 금고 | 레거시 `SecureVaultService`, AV3 별도 | AV3 테스트 다수 | AV3 앱 적용 및 기존 데이터 호환 미확인 | PARTIAL |
| 보안 삭제 | SecurityLab/StorageSanitizer 계층 | 시뮬레이션·dry-run 존재 | 실매체 잔존성·하드웨어 검증 없음 | PARTIAL |
| 장치 Purge | WinPE 실행·증거 테스트 | 테스트 존재 | 장치별 실제 Purge 확인 없음 | PARTIAL |
| 업데이트 검사 | ZIP/manifest 검사기 | 수정 전 재현, 수정 후 회귀 PASS | 앱 E2E와 배포 경로 미확인 | PARTIAL |
| 업데이트 적용 | 설치 서비스 | 경계 패치 | 설치/중단/재개 E2E 미확인 | PARTIAL |
| 런타임 신뢰 | `RuntimeTrustState` | 코드 경로 확인 | 서명/무서명 실제 표시 검증 없음 | PARTIAL |

여기서 PASS는 해당 검증 명제에만 적용한다. 구현 존재를 제품 수준 완성도 퍼센트로 환산하지 않는다.
