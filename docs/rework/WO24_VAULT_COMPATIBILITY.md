# WO24 — Vault compatibility

Baseline: `07acfd724aae16e165afc259ba09dbac537c5eee`.
Branch: `codex/pccare-wo24-isolated-install-recovery-qualification`.
Executable source: `e996082e9656d26564d88d371f6ea992095d2b75`.

Reuse the WO20Probe v5 fixture and authenticated full-file exports.
Retain 0B, 4KiB, 1MiB and 4MiB inputs.
The WO24 synthetic flow checks the vault before and after applying a signed fixture package.
The previous 15 publication interruption cases also remain in CI.
Raw plaintext, passwords and recovery codes remain outside public evidence.
Installed guest, reboot and new recovery-code execution: NOT TESTED.
