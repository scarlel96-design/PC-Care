# E-12 XChaCha24 Crypto Contract

**Suite:** `Av3AeadAlgorithmId.XChaCha20Poly1305Ietf24` (wire id **3**)  
**Primitive:** XChaCha20-Poly1305 IETF with **24-byte extended nonce**  
**Status:** **implementation candidate** — `XChaCha24Implemented=false`

## Binding rules

| Field | Rule |
|-------|------|
| Algorithm id | Authenticated in AAD for activation payload and metadata.root |
| Nonce | 24 bytes; fixture nonces derived via `Av3AeadNoncePolicy.FixtureNonce` (TEST ONLY) |
| Key material | Fixture keys via `Av3AeadKeyMaterialPolicy.DeriveFixtureKey` (labels only in vectors) |
| Dispatch | `Av3AeadDispatch.Resolve` → `Av3XChaCha24Aead.Instance` |
| Downgrade | `Av3CryptoDowngradeGuard` rejects mixed suite ids and ChaCha12 when `xchacha24RequiredPolicy=true` |
| Public errors | `UnlockValidationException.PublicMessage` — no secret-bearing detail |

## Production policy (writer still disabled)

- `Av3CryptoPolicy.ProductionWriteRequiresXChaCha24 = true`
- `Av3CryptoPolicy.XChaCha24AeadCodePresent = true`
- `Av3PhaseGate.XChaCha24Implemented = false` until explicit E-12.1 sign-off record

## Evidence

- `Av3PhaseE12Tests` — vector auth, tamper negatives, downgrade, read-only chain, dry-run revalidation
- `test-vectors/av3/xchacha24/` — vector id manifest (no secrets)