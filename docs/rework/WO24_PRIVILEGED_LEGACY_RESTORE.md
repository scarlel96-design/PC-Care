# WO24 — Privileged legacy restore

Baseline: `07acfd724aae16e165afc259ba09dbac537c5eee`.
Branch: `codex/pccare-wo24-isolated-install-recovery-qualification`.
Executable source: `e996082e9656d26564d88d371f6ea992095d2b75`.

Reuse LegacyRecoveryPlan, LegacyRestoreApproval and LegacyApprovalLifecycle.
Keep RequestId reservations separate from restored-file verification.
Retain UnknownContent and original schema 1 journals.
The complete WO23 approval interruption matrix remains in the WO24 workflow.
These synthetic process checks do not establish an elevated administrator restoration session.
Actual approved schema 1 restoration and UAC: NOT TESTED.
