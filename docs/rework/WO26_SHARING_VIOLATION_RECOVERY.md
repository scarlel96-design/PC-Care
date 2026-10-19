# WO26 — Sharing Violation Recovery

Baseline: `1a4fd0811f263f35a94389791860d374c710759f`.
Verification source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
Branch: `codex/pccare-wo26-transaction-recovery-volume-staging`.

## Actual synthetic Windows cases
The harness opens read, write and exclusive handles in separate child processes.
The apply process records native error 32 and its mutation state.
The read-only target records native error 5.
The parent releases each holder before checking original SHA-256 values.

A separate incomplete transaction supports the reconciliation matrix.
An exclusive holder blocks read-only reconciliation.
After release, an explicit recovery request rechecks authenticated inputs.
The harness separately replaces the target identity, backup, package or current content.
Each changed input must block the new verified recovery path.
No timed automatic retry loop is installed.
An access denial or signature rejection is not a transient sharing retry.

The old WO23 replacement IOException remains UNKNOWN.
These native error cases do not identify its historical cause.
The request's share contract is known from boundary code.
The external holder's operating-system state is not inferred from that contract.

## Final automated qualification

Source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
CI: [39166559052 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39166559052).
App: 1429 PASS / 0 FAIL / 111 SKIP.
New WO26: 21 PASS, included in app total.
SecurityLab: 36 PASS, separate.
Restore and Release: 0 warnings / 0 errors.
Actual product GUI, UAC, independent SID and reboot remain NOT TESTED.
