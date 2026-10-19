# WO26 — Ui Integration

Baseline: `1a4fd0811f263f35a94389791860d374c710759f`.
Verification source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
Branch: `codex/pccare-wo26-transaction-recovery-volume-staging`.

## Connected product operations
The existing window retains status refresh and trusted administrator recovery request actions.
A new read-only file-state action calls UpdateRecoveryCoordinator.Reconcile through Task.Run.
It displays incomplete transactions separately from original-byte preservation.
The original-state closure code has an explicit Korean message.
A new diagnostic-copy action calls the fixed allowlist exporter and the Windows clipboard API.
Buttons disable during their asynchronous action and re-enable after completion or failure.
Existing legacy approval review and explicit consent stay separate.

The GUI source contract checks actual call sites and automation IDs.
This contract is not a GUI execution result.
High DPI, narrow layout, keyboard focus, screenreader and reduced-motion operation remain NOT TESTED.
No screen-control session is started or automatically reconnected.

## Final automated qualification

Source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
CI: [39166559052 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39166559052).
App: 1429 PASS / 0 FAIL / 111 SKIP.
New WO26: 21 PASS, included in app total.
SecurityLab: 36 PASS, separate.
Restore and Release: 0 warnings / 0 errors.
Actual product GUI, UAC, independent SID and reboot remain NOT TESTED.
