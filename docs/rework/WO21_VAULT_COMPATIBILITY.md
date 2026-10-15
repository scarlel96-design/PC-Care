# WO21 — VAULT COMPATIBILITY

Status: PARTIAL. Phase 1: NO-GO.

Base HEAD: `1d93600ea5c6214e23051641ad10c06a667f0866`.
Branch: `codex/pccare-wo21-signed-install-recovery-qualification`.
Read `evidence/wo21/VERIFICATION.txt` for final tested-source and publication receipts.

## Actual encrypted fixture

The existing WO20Probe creates SecurityLab v5 with synthetic content only.
The four input sizes remain 0 B, 4 KiB, 1 MiB and 4 MiB.
Expected SHA-256 values are captured before ImportBytes clears its input buffer.
Each separate checker unlocks the vault and performs authenticated full extraction.
It compares every extracted SHA-256 and file count.
The new recovery-code checker uses a DPAPI-protected fixture-only code.
It never logs that code or the password.
It records the authenticated format and remaining recovery-slot count.

## Observed local matrix

The completed matrix contains 134 commands and 13 parent-killed workers.
It contains 46 full four-file extraction checks, including seven recovery-code sessions.
All 184 original/extracted SHA-256 pairs match.
The matrix includes normal updates, update interruption, reviewed legacy restoration and installation mismatch.
An independent process reopens each checked vault.
This is synthetic Windows file-system compatibility evidence.
It is not a reboot, independent SID or physical power-loss result.

No vault format, password-change policy, recovery-slot implementation or AV3 gate was changed.
Recovery metadata is opened through the existing authenticated service.
Separate forensic validation of every raw recovery-slot field remains outside this result.

## Verification source

Final tested source:`930ed7907daaa95cf15040d130c580248e64bf61`.
Windows CI:[38669715643](https://github.com/scarlel96-design/PC-Care/actions/runs/38669715643).
Final publication adds reports and evidence only.
