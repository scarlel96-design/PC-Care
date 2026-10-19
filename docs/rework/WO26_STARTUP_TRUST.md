# WO26 — Startup Trust

Baseline: `1a4fd0811f263f35a94389791860d374c710759f`.
Verification source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
Branch: `codex/pccare-wo26-transaction-recovery-volume-staging`.

## Preserved policy
The product startup policy continues to verify retained signed history.
Fresh and Legacy installations remain explicitly unattested.
Unsigned or corrupted declarations do not receive verified status.
Unlisted EXE and DLL files require review after signed installation qualification.
Ordinary data files remain preserved.
A journal path outside the signed scope returns review instead of an uncaught dictionary lookup error.

Terminal transaction closure is not a full-installation trust decision.
The coordinator continues to query startup trust separately.
The managed launcher is not a native pre-CLR boundary.

## Freshness limits
Retained history is not an independent witness.
Entire-prefix deletion and joint installation/journal rollback remain unqualified.
A same-user replayable state file does not add an independent security boundary.
Administrator or whole-machine rollback protection remains unproven.

## Final automated qualification

Source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
CI: [39166559052 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39166559052).
App: 1429 PASS / 0 FAIL / 111 SKIP.
New WO26: 21 PASS, included in app total.
SecurityLab: 36 PASS, separate.
Restore and Release: 0 warnings / 0 errors.
Actual product GUI, UAC, independent SID and reboot remain NOT TESTED.
