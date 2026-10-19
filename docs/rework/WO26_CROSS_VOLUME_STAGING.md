# WO26 — Cross Volume Staging

Baseline: `1a4fd0811f263f35a94389791860d374c710759f`.
Verification source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
Branch: `codex/pccare-wo26-transaction-recovery-volume-staging`.

## Design decision
A uses a protected target-volume transaction area.
B copies authenticated inputs from existing staging into that protected area.
Both designs require directory pins, owner/DACL verification, file identity, full hash and durable publication/recovery binding.
A created directory alone does not establish those conditions.
C rejects cross-volume new-file publication before transaction creation and installation mutation.

WO26 retains C and `PUBLISH-SAME-VOLUME-REQUIRED`.
No production A/B path is enabled.
An isolated target-volume copy prototype with complete durable recovery is NOT IMPLEMENTED.
Its staging creation, copy, cleanup and interrupted-copy qualification are NOT TESTED.
The existing strict production work root remains fixed.
WO26 does not add a second privileged staging authority.

The previous hosted-runner real-volume refusal matrix is retained in CI.
A/B needs a separately reviewed protected-root allocation and recovery contract.
This limitation is open product work, not a successful cross-volume deployment claim.

## Final automated qualification

Source: `bd2d6d1aa62cfcfb556e8a8b32dd9aa28cd3f262`.
CI: [39166559052 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39166559052).
App: 1429 PASS / 0 FAIL / 111 SKIP.
New WO26: 21 PASS, included in app total.
SecurityLab: 36 PASS, separate.
Restore and Release: 0 warnings / 0 errors.
Actual product GUI, UAC, independent SID and reboot remain NOT TESTED.
