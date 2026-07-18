# Astra Vault XChaCha24 Migration Plan (Phase E-5 — plan only)

**Status:** DOCUMENTED — **NOT IMPLEMENTED**  
**S-Class:** NOT YET SATISFIED until TARGET suite is live

## CURRENT (implemented)

- `cipher_suite_id = 1` → .NET `ChaCha20Poly1305` with **12-byte nonce**
- Documented as **BELOW S-CLASS transitional** (`AstraCryptoSuitePolicy`)
- Golden vectors LOCKED under `test-vectors/av3/` for CURRENT suite

## TARGET (S-Class)

- **XChaCha20-Poly1305** with **24-byte extended nonce** (libsodium-style HChaCha derivation)
- Reduces nonce reuse risk under high-volume segment encryption

## Library selection criteria

- Audited implementation (e.g. libsodium / NSec / vetted BCL extension)
- Constant-time expectations for AEAD paths
- FIPS posture documented if applicable
- License compatible with commercial PC Care distribution
- Windows x64 primary; ARM64 roadmap noted

## Dependency risk

- Native vs managed interop
- Servicing and CVE response SLA
- Single-vendor lock-in mitigation

## Test vector strategy

- New golden corpus `test-vectors/av3/xchacha24/` (future) — do not mutate LOCKED CURRENT vectors
- Cross-validate against reference implementation (sodium test vectors)
- KAT: nonce edge lengths, AAD binding, tag failure cases

## Suite id change

- Propose `cipher_suite_id = 3` (or next free id) for XChaCha20-Poly1305-24
- `AstraSuiteIds` + format doc update in implementation phase
- Activation/metadata AAD unchanged except `alg_id` / suite field

## Backward compatibility

- Read path: unlock must accept CURRENT + TARGET during transition
- Write path: new containers TARGET-only after cutover flag
- No in-place re-encryption of user vaults without explicit wizard (Phase H/F scope)

## Migration strategy

1. Implement TARGET AEAD behind feature flag (off).
2. Dual-read validator in read-only pipeline.
3. Writer emits TARGET only when `XChaChaProductionEnabled` (future gate) true.
4. Optional user-initiated rewrap wizard — not automatic.

## Nonce uniqueness proof

- Document counter + random subfield layout for 24-byte nonces
- HKDF domain separation per object/segment unchanged

## Fallback policy

- Fail-closed: no silent downgrade to 12-byte nonce on write
- If TARGET unavailable at runtime, writer refuses commit (not CURRENT write)

## Release gate

- All golden + FI matrices pass on TARGET
- External review sign-off on crypto delta
- **E-5 does not implement XChaCha** — implementation is a separate phase

## Related

- `ASTRA_VAULT_CRYPTO_MODEL.md`
- `ASTRA_VAULT_FORMAT_TEST_VECTORS.md`