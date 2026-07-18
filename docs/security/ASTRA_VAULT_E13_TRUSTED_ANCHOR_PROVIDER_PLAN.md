# E-13 Trusted Anchor Provider Plan

**Phase:** E-13 Trusted Anchor Provider Implementation (not production writer enable)  
**Date:** 2026-07-06

## Scope

- Implement production-disabled trusted anchor provider contracts and harness/integration tests.
- **Production design target:** Hybrid (machine-local candidate + external digest-only witness).
- **Out of scope:** service/UI/import/export wiring, migration, user vault paths, spd-vault on-disk changes, automatic repair.

## Mandatory limitations

- **same-disk untrusted anchor alone cannot prove full-vault rollback resistance**
- **full vault rollback closure requires trusted monotonic anchor**
- **S-Class remains NOT YET SATISFIED** until anchor sign-off + XChaCha24 production gate + production verification

## Provider strategy (final target)

| Provider | E-13 status |
|----------|-------------|
| Same-disk local | Documented insufficient — cannot close B-1 alone |
| Machine-local candidate | Interface + test double + fail-closed binding |
| External witness candidate | API contract + stub only (no live server) |
| Hybrid coordinator | Production target design; harness verification |
| Null / unavailable | Fail-closed; production enable NO-GO |

## Gates (unchanged)

| Flag | Value |
|------|-------|
| Production Writer | **NOT AUTHORIZED** |
| Writer Enable Readiness | **NO-GO** |
| `ProductionEnableAuthorized` | **false** |
| `ExternalReviewCompleted` (code) | **false** |
| `ProductionAnchorImplemented` | **false** |
| `XChaCha24Implemented` | **false** (approved candidate only) |
| `SClassTargetSatisfied` | **false** |

## Next phase

**E-13.1 Trusted Anchor Sign-off** — may promote `ProductionAnchorImplemented` only after explicit sign-off review (not in E-13).