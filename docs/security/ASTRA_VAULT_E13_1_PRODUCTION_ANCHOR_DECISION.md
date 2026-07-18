# E-13.1 Production Anchor Decision (B-1)

**Gate:** B-1 Production Anchor adjudication — not `ProductionAnchorImplemented=true` promotion

## Decision

| Field | Value |
|-------|-------|
| **B-1 Production Anchor** | **PARTIAL** / **SIGNED CANDIDATE** |
| Harness + stub trusted anchor (E-13) | **SIGNED** for test/harness paths only |
| Live external monotonic witness | **NOT DEPLOYED** |
| `ProductionAnchorImplemented` | **false** (unchanged) |
| `TrustedMonotonicProductionAnchorImplemented` | **false** |
| `TrustedAnchorProviderSignoffSignedCandidate` | **true** |
| `B1ProductionAnchorSignedCandidateOnly` | **true** |
| Full vault rollback — harness | **CLOSED** |
| Full vault rollback — production | **PARTIAL** |
| Production Enable | **NO-GO** |
| S-Class | **NOT YET SATISFIED** |

## Mandatory limitations (locked)

- **same-disk untrusted anchor alone cannot prove full-vault rollback resistance**
- **full vault rollback closure requires trusted monotonic anchor**

## Rationale

1. **Hybrid** remains production design target (`Av3TrustedAnchorPolicy.ProductionDesignTarget`).
2. **Same-disk local** anchor cannot close B-1 alone.
3. **Machine-local** candidate is production-shaped but **partial** for full-disk rollback without external witness.
4. **External witness** contract and stub are **signed**; **no live server** → cannot set `ProductionAnchorImplemented=true`.
5. **Hybrid** coordinator enforces offline no writer promotion and external unavailable → production NO-GO.

## Prohibitions (unchanged)

No production writer enable; no service/UI/import/export wiring; no migration; no user vault or spd-vault on-disk mutation; no automatic repair; no S-Class or production-ready claims.

## Next

**E-14 Disk Durability Review** — **COMPLETE** (candidate only; `ActualDiskDurabilityReviewed=false`) · **E-14.1 sign-off** · **Live External Witness Implementation** (future gate) · Writer enable remains **NO-GO**