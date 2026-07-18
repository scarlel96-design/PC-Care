# E-11 Anchor Threat Model (LOCKED)

**Phase:** E-11 Production Anchor Closure — **not** production writer enable.

## Full vault rollback attack model

1. **Whole-tree restore:** An attacker or backup tool restores the **entire** AV3 vault directory (header copies, metadata.root, journal, object segments) to an older consistent snapshot. Cryptographic fields remain internally consistent at the rolled-back generation.

2. **Coordinated rollback:** Header, metadata.root, journal, and object segments all regress together — local partial-generation checks may not detect this if no independent witness exists.

3. **Same-disk anchor rollback:** An anchor file stored on the **same** disk/volume as the vault can be restored together with the vault. **same-disk untrusted anchor alone cannot prove full-vault rollback resistance.**

4. **Local disk trust limit:** When only local disk is trusted and both vault and anchor are restorable as a unit, **detection of full-vault time travel may be impossible** without an external or TPM/machine-bound trusted monotonic witness.

## Scope separation

| Scenario | Anchor role |
|----------|-------------|
| Local generation rollback | Harness + classifier can flag when anchor generation disagrees with authenticated head |
| Stale header | Verification vs witness digest / generation |
| Partial rollback | In-vault consensus + anchor witness (harness) |
| Full vault rollback | Requires **trusted monotonic anchor** (external or machine-bound) — **PARTIAL** in E-11 harness |
| Machine-local trusted anchor | Design + harness file monotonic counter (test-only) |
| External/trusted monotonic anchor | Documented; **not** production-enabled in E-11 |

## Mandatory statements

- **same-disk untrusted anchor alone cannot prove full-vault rollback resistance**
- **full vault rollback closure requires trusted monotonic anchor**
- **S-Class remains NOT YET SATISFIED until anchor + XChaCha24 + production verification are complete**

## Posture (E-11)

- Production Writer: **NOT AUTHORIZED**
- `ProductionAnchorImplemented`: **false** (harness **candidate** only)
- Legacy spd-vault: **BELOW S-CLASS**, on-disk **unchanged**