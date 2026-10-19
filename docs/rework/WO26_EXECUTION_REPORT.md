# WO26 — Execution Report

Baseline: `1a4fd0811f263f35a94389791860d374c710759f`.
Verification source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
Branch: `codex/pccare-wo26-transaction-recovery-volume-staging`.

## Implementation summary
The work corrects unnecessary writes during original-state rollback.
It adds read-only reconciliation and an explicitly verified product recovery entry point.
It preserves the closure reason across restart and product UI display.
It reduces retained metadata buffers and adds bounded history profiling.
It restricts deployment dependency paths and isolates external diagnostics from sensitive fields.
It retains the cross-volume fail-before-mutation policy.
Existing update, vault, cleanup, delete and production trust gates remain in place.

No third-party package version changes were required.
The work reuses WindowsFileBoundary, the existing update journal and rollback engine, .NET cryptography and WinUI.
The original locked-later-destination test name and byte checks remain.
Its obsolete RecoveryRequired expectation now checks Restored plus PreMutationRejected, verified originals and no mutation.
No existing test is removed or newly skipped.

## Evidence policy
Accept only the final frozen-source regression and CI artifacts.
Preserve initial failures, cancelled CI and invalidated source-change runs separately.
Source-copy rollback is distinct from installation transaction recovery and vault extraction.
Actual results and final publication identities are appended after verification.

## Verified outcomes

The final CI passed all retained workflow stages.
The Windows ACL matrix contains 9 passing cases.
The PE Authenticode matrix contains 15 passing cases.
These disposable-runner cases do not qualify independent SID, interactive UAC or complete signed Bridge deployment.
Synthetic v5 readers reopened 28 times and verified 112 complete file-hash pairs.
The new recovery process matrix verifies three transaction termination points.
The inherited DuringRollbackCopy checkpoint runs after the file copy returns.
Mid-byte-copy termination of large files remains NOT TESTED in that matrix.

## Preserved intermediate results

CI 39165657873 was cancelled after the execution source changed.
CI 39165942479 failed because the new WO25Probe source was omitted from its commit.
That run used the corrected engine but lacked the probe's new result fields.
Commit bd2d6d1 publishes the previously tested probe source.
The final CI validates that complete source.
The failed process output and initial failed test result remain in the evidence directory.

## Final automated qualification

Source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
CI: [39166559052 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39166559052).
App: 1429 PASS / 0 FAIL / 111 SKIP.
New WO26: 21 PASS, included in app total.
SecurityLab: 36 PASS, separate.
Restore and Release: 0 warnings / 0 errors.
Actual product GUI, UAC, independent SID and reboot remain NOT TESTED.
