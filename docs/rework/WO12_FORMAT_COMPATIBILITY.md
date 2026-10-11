# WO-12 format compatibility

Branch: `codex/pccare-vault-reliability-product-ux`

Baseline: `ba9a65f7f035262ada7ba53252c103921f87a32c`

Overall status: **PARTIAL**. Phase 1: **NO-GO**.

| Format | Current route | WO-12 evidence | Status |
|---|---|---|---|
| Legacy spd-vault | SecureVaultService legacy manifest path | Create/import preserves source; export collision preserves destination; existing regression suite | PARTIAL |
| SecurityLab v4 | Existing compatible Lab reader | Existing SecurityLab regression; no newly generated v4 fixture in WO-12 | PARTIAL |
| SecurityLab v5 | LabVaultService and product adapter | Create/unlock, folder round trip, recovery code, password change, crash readers | PARTIAL |
| AV3 read-only | Separate AV3 implementation | Existing regression and six false approvals; no new dedicated AV3 fixture round trip | PARTIAL |
| AV3 production Writer | Disabled | Six approval values remain false | BLOCKED |

The adapter reports the authenticated format after unlock.
The UX no longer presents AV3 as the active format for Lab data.
v5 recovery codes wrap the master key. Legacy v4 hash-only recovery evidence cannot reconstruct that key.
The implementation does not automatically migrate formats.
Both-format detection and historical fixture coverage require broader compatibility qualification.
