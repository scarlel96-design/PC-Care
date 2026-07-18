# E-11.1 Trusted Monotonic Anchor Decision

**Gate:** Trusted Anchor Decision (not production writer enable)  
**Date:** 2026-07-06

## Mandatory limitations (locked)

- **same-disk untrusted anchor alone cannot prove full-vault rollback resistance**
- **full vault rollback closure requires trusted monotonic anchor**
- **S-Class remains NOT YET SATISFIED until anchor + XChaCha24 + production verification are complete**

## Strategy comparison

| Candidate | Full vault rollback | Privacy | Secrets non-leak | Offline | Device/account recovery | S-Class B-1 closure |
|-----------|---------------------|---------|------------------|---------|-------------------------|---------------------|
| Same-disk local anchor | **Insufficient** | Low path risk if digest-only | Yes (harness) | Yes | N/A | **No** — production S-Class use **forbidden** |
| Machine-local trusted (TPM/DPAPI) | **Partial** — disk image rollback | Medium — machine binding metadata | Yes if digest-only | Yes | Reinstall/backup policy required | **Possible** with FI + sign-off |
| External trusted monotonic | **Strongest** | Server/account policy | Yes with digest-only witness | Degraded without sync | Account recovery required | **Yes** — primary production target |
| Hybrid (machine + external) | **Strong** | Configurable | Yes | Split offline/online | Complex | **Yes** — recommended long-term |

## Decision

| Field | Value |
|-------|-------|
| **B-1 Production Anchor** | **PARTIAL** / **SIGNED CANDIDATE** |
| Harness anchor (E-11) | **SIGNED** — approved for harness/local witness only |
| Trusted monotonic production anchor | **NOT IMPLEMENTED** |
| `ProductionAnchorImplemented` | **false** (unchanged) |
| Full vault rollback coverage | **PARTIAL** |
| Production Writer | **NOT AUTHORIZED** |
| Writer Enable Readiness | **NO-GO** |
| `ProductionEnableAuthorized` | **false** |
| `ExternalReviewCompleted` (code) | **false** |

## E-13 / E-13.1 update

- **Hybrid provider** production **design target** — E-13 implementation **COMPLETE**; E-13.1 sign-off **COMPLETE**.
- B-1: **PARTIAL / SIGNED CANDIDATE**; `ProductionAnchorImplemented=false`; live external witness **없음**.
- Harness rollback matrix **CLOSED**; production full-vault rollback **PARTIAL**.

## Recommended next track

1. **Disk durability review**
2. **Live external witness** (future implementation gate — not E-13.1)
3. **XChaCha24** — E-12.1 signed candidate; `XChaCha24Implemented=false`

**Not in scope:** service/UI wiring, migration, user vault paths, spd-vault on-disk changes.