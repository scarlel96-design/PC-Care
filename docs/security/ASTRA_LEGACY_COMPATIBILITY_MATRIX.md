# Legacy Compatibility Matrix

| Format | Marker | Read | Write | Migration to av3 |
|--------|--------|------|-------|-------------------|
| spd-vault-v1/v2 | `SPDVLT1` envelope | ✅ App | ⚠️ legacy only | Phase H copy-on-write |
| spd-vault-v3 | `vault.svdb` + v3 manifest | ✅ App | ⚠️ legacy only | Phase H |
| av3 secure container | `AVLT` locator | Phase C parser | Phase E writer | N/A |

## Migration rules (내역.txt §15)

- UNKNOWN legacy → inventory 먼저  
- In-place 변환 금지  
- 새 vault 검증 후에만 legacy 삭제  
- 사용자 명시 승인 + migration report  
- 실패 시 legacy 유지