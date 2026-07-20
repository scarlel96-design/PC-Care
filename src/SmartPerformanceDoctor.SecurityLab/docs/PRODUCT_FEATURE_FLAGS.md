# 제품 기능 플래그 (50.3.0)

## 런타임

| 이름 | 기본 (제품 App) | 의미 |
|------|-----------------|------|
| master | ON | SecurityLab 호스트 활성 |
| VaultV4 | ON | 신규 금고 v4 |
| ShredNext | ON | 보안 삭제 Lab 엔진 |
| Migration | ON | v3→v4 마이그레이션 API |

## 비활성화

```
set PCCARE_SECURITYLAB=0
```

App 생성자에서 host bind를 건너뜁니다. (레거시 v3 전용 동작)

## 코드

- `ProductFeatureFlags.EnableProductHost(...)` — App 시작
- `LabProductGate.EnsureEnabled` — 어댑터 진입 시
- `SecureVaultLabBackend` — 금고 UI 백엔드
- Lab 단위 테스트는 host 미바인딩 → 플래그 false 유지
