# E-12 XChaCha24 Downgrade Model

**Posture:** fail-closed — mixed algorithms and downgrade attempts reject decrypt.

## Guard: `Av3CryptoDowngradeGuard`

| Check | Behavior |
|-------|----------|
| Zero suite id | `UnlockValidationException` |
| Header suite ≠ payload suite | `UnlockValidationException` |
| Unknown suite id | `UnlockValidationException` via `Av3CryptoPolicy.AllowsDecrypt` |
| `xchacha24RequiredPolicy=true` + ChaCha12 transitional | `UnlockValidationException` |
| `IsDowngradeAttempt(declared, aadBound)` | true when ids differ or declared id unknown |

## Transitional vs target

| Suite | Id | S-Class |
|-------|-----|---------|
| ChaCha20-Poly1305 12-byte nonce | `ChaCha12Transitional` (1) | **BELOW S-CLASS** — read path only where policy allows |
| XChaCha20-Poly1305 24-byte nonce | `XChaCha20Poly1305Ietf24` (3) | **TARGET** — candidate implemented; sign-off pending |

## Writer enable linkage

- Future production write requires XChaCha24 (`Av3CryptoPolicy.ProductionWriteRequiresXChaCha24`)
- `XChaCha24Implemented` remains **false** until E-12.1 sign-off
- Downgrade tests: `Av3PhaseE12Tests.E12_XChaCha24_DowngradeAttempt_Rejected`, `E12_XChaCha24_MixedAlgorithmChain_Rejected`, stability theory ×3