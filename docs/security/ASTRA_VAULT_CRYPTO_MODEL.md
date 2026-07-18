# Astra Vault Crypto Model (Target)

> **현재 구현 상태:** AV3는 **NOT PRODUCTION / READ-ONLY VALIDATION** 단계이다.  
> Writer / journal / migration **금지**. spd-vault 레거시는 **BELOW S-CLASS**.

## Password

- 저장 금지. Argon2id만 (SHA-256(password) 단독 금지).
- 최소: memory ≥ 64 MiB, iterations ≥ 3, parallelism ≥ 1 (프로필별 상향).
- `KdfBelowMinimum` → reject, UI에 보안 등급 하향 표시.

## Key hierarchy

```
Password
  → Argon2id → KEK
  → unwrap VMK (AEAD, AAD=slot+container+generation)
  → activation payload AEAD (AAD binds container, vault_id, copy_id, generation, suite, metadata digest)
  → metadata-root AEAD (Phase D read-only; AAD binds activation digest + ciphertext digest + logical id)
  → HKDF-SHA256 domains:
        header, metadata, content(DEK), index, journal, recovery, audit
  → per-object: HKDF(VMK or DEK, object_id, "astra-segment")
```

## AEAD — CURRENT vs TARGET

| Layer | Status |
|-------|--------|
| **CURRENT (implemented)** | `cipher_suite_id=1` → .NET **ChaCha20-Poly1305** with **12-byte nonce** (`AstraCryptoSuitePolicy`) — **BELOW S-CLASS transitional** |
| **TARGET (S-Class)** | **XChaCha20-Poly1305** with **24-byte nonce** — **NOT YET SATISFIED** |
| **Optional** | AES-256-GCM (suite id 2) |

- 모든 blob: ciphertext + tag; decrypt 전 tag verify (fail-closed).
- 문서/enum 이름 `XChaCha20Poly1305`는 suite id 1 **별칭**이며 CURRENT 구현은 12-byte nonce임.

## AAD (mandatory fields)

`format_version | container_id | domain_label | object_id | segment_id | generation | alg_id | stored_length | parent_commitment`

## Legacy mapping

| Legacy | v3 |
|--------|-----|
| vaultKey | VMK |
| per-file DEK | segment key via HKDF |
| metadataKey | metadata domain key |
| macKey | audit / manifest chain key |