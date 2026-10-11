# WO-10 sandbox execution

Status: PARTIAL.

Branch: `codex/pccare-reference-correction-safe-recovery`.
Baseline: `7e4dc94c7c8ed9260e203f99dbf57f5a1e753e20`.
Verified executable source: `c23802b2adc7be63c8c15ab6cd313ec9be0a05db`.
Source tree: `752a647f89d6cced6637c1224e5e2dcae9618776`.
Phase 1: NO-GO.

## Actual Windows filesystem results

The harness creates separate synthetic fixture directories under its test output directory.
Each fixture contains 4096 deterministic bytes and a private journal authentication key.
The directory ACL disables inheritance and grants the current owner and SYSTEM access.
Execution records Prepared, renames the opened file, verifies bytes, and records Quarantined.
Recovery records RollingBack, restores the opened file, verifies bytes, and records Restored.
No actual user Temp, Downloads, Windows directory, or vault is a target.

The separate-process harness terminated workers at six checkpoints.
Every recovery used a new process and returned Restored with BytesVerified=true.
All before and after SHA256 values matched.
Hardlink and junction substitution returned SANDBOX_BLOCKED IOException, exit 3.
Local symbolic-link creation was unavailable. That local scenario remains NOT TESTED.
The first restricted hardlink setup attempt failed. The authorized rerun passed.
See LOCAL_SANDBOX.json and SANDBOX_EXECUTION_RESULT.txt.

## Automatic regression

Tests cover changed bytes, replaced identities, expired consent, foreign consent, locks, collisions, journal tampering, and cancellation.
Tests also cover concurrent execution and directory replacement while leases remain open.
The application dependency test confirms that the product has no sandbox executor path.
Tests preserve conflicting originals and uncertain quarantine files.
The six forced terminations are distinct from same-process exception tests.
Physical power failure, reboot, and installed product recovery remain NOT TESTED.

## CI evidence

Windows CI run 38113296563 passed the six process-termination checkpoints.
Read evidence/wo10/ci/SANDBOX_RESULT.json for individual exit codes and restored-byte checks.
CI rejected hardlink, junction, and symbolic-link substitutions.
The standalone tool remains absent from the product dependency graph.
