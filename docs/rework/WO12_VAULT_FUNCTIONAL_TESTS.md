# WO-12 vault functional tests

Branch: `codex/pccare-vault-reliability-product-ux`

Baseline: `ba9a65f7f035262ada7ba53252c103921f87a32c`

Overall status: **PARTIAL**. Phase 1: **NO-GO**.

## New automated cases

WO12VaultReliabilityTests adds 17 cases. Four are export-size theory inputs: 0, 1, 257 and 1,048,577 bytes.
The remaining cases cover existing destinations, foreign temporary files, hash failure, cancellation, file locks and directory identity.
They also cover folder hierarchy, source preservation, password recovery, scan cancellation, journal preservation, approvals and duplicate sessions.
The folder fixture contains four files. It includes Korean names, Unicode, empty content and a/ab sibling prefixes.
Every exported file is compared byte-for-byte. Every source file remains present.
The folder test exercises the bulk-export API. It does not prove graphical multi-selection.
The legacy collision test preserves its sentinel destination.
The v5 password test rejects the wrong password, uses a recovery code, changes the password and verifies exported bytes.

| Requested behavior | Evidence | Status |
|---|---|---|
| Create, unlock, wrong password, reopen | Headless real synthetic vault calls | PASS |
| File/folder import and hierarchy | Four-entry v5 fixture and legacy import | PASS for tested inputs |
| Bulk API export and collision | Byte comparisons and sentinel | PASS |
| Password change and recovery code | v5 normal path | PASS; interruption NOT TESTED |
| Automatic lock | Existing coordinator/regression | PARTIAL; live GUI NOT TESTED |
| Metadata/cipher/tag tamper | Existing regression suite | PARTIAL; all failure timings not independently qualified |
| Thousands of entries / long paths | No new load fixture | NOT TESTED |
| Disk full, permission denial, read-only media | No dedicated real-environment test | NOT TESTED |
| Mid-export cancellation | API checks exist; pre-cancel test only | PARTIAL |
| Concurrent create/import/export/lock | Independent-session lease test only | PARTIAL |
| Physical power interruption | No disposable VM/device trial | NOT TESTED |

## Counts

Local final application result: 1,019 PASS / 111 SKIP / 0 FAIL / 1,130 total.
Baseline application result: 1,002 PASS / 111 SKIP / 0 FAIL.
SecurityLab: 36 PASS / 0 SKIP / 0 FAIL, separate project.
The 17-case increase does not represent 17 independent device environments.
The first broad run had one reparse-message regression. Production validation was corrected and the full suite passed again.
