# Secure Delete Policy

- Import 기본: **원본 잠금·삭제** (`sealOrigin=true`, UI에서 해제 가능).
- 원본 잠금·삭제: UI 체크박스 명시 동의 + import 검증 후만.
- SSD/TRIM: 완전 삭제 보장 불가 — 설정·도움말에 문서화.
- 레거시 `SecureVaultOriginSealService`: 고급 옵션으로만 호출.