# Astra Vault / 아스트라 금고 — 보안 문서

제품 사양: 워크스페이스 `내역.txt` (VeraCrypt급 secure container 목표).

| 문서 | 용도 |
|------|------|
| [ASTRA_VAULT_GAP_REPORT.md](./ASTRA_VAULT_GAP_REPORT.md) | 현재 PCCare 금고 vs 목표 갭 |
| [ASTRA_VAULT_ARCHITECTURE_AUDIT.md](./ASTRA_VAULT_ARCHITECTURE_AUDIT.md) | 코드·저장소 감사 |
| [ASTRA_VAULT_CRYPTO_MODEL.md](./ASTRA_VAULT_CRYPTO_MODEL.md) | KDF·키 계층·AEAD |
| [ASTRA_VAULT_THREAT_MODEL.md](./ASTRA_VAULT_THREAT_MODEL.md) | 위협·완화 |
| [ASTRA_VAULT_HARDENING_PLAN.md](./ASTRA_VAULT_HARDENING_PLAN.md) | Phase A–J 로드맵 |
| [ASTRA_SECURE_CONTAINER_FORMAT.md](./ASTRA_SECURE_CONTAINER_FORMAT.md) | v3 컨테이너 바이트 스펙(초안) |
| [ASTRA_VAULT_TRANSACTION_MODEL.md](./ASTRA_VAULT_TRANSACTION_MODEL.md) | 저널·커밋 상태 |
| [ASTRA_LEGACY_COMPATIBILITY_MATRIX.md](./ASTRA_LEGACY_COMPATIBILITY_MATRIX.md) | spd-vault → av3 이전 |
| [ASTRA_SENTINEL_AI_POLICY_CONTRACT.md](./ASTRA_SENTINEL_AI_POLICY_CONTRACT.md) | Sentinel·Policy·Broker |
| [ASTRA_UI_SECURITY_STATE_MODEL.md](./ASTRA_UI_SECURITY_STATE_MODEL.md) | UI↔crypto 상태 |
| [ASTRA_LOGGING_POLICY.md](./ASTRA_LOGGING_POLICY.md) | 보안 로그 금지 항목 |
| [ASTRA_SECURE_DELETE_POLICY.md](./ASTRA_SECURE_DELETE_POLICY.md) | 원본 삭제 정책 |
| [ASTRA_TEMP_FILE_POLICY.md](./ASTRA_TEMP_FILE_POLICY.md) | 임시·export 경로 |

구현 코드: `src/SmartPerformanceDoctor.AstraVault/`