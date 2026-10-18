# WO25 — Vault compatibility

Baseline: `71d8f3ba85804a61157c0acf553bb37ccf6c07ad`.
Branch: `codex/pccare-wo25-installation-trust-volume-hardening`.

The workflow retains the existing WO20-WO24 authenticated v5 extraction matrix.
It reopens synthetic encrypted vaults across updater application and recovery.
It checks 0-byte, 4-KiB, 1-MiB and 4-MiB payload hashes.
The normal-update path adds a before/after authenticated reopen pair.
The final CI evidence reports actual reopen and hash-pair counts.
These counts are separate from app test totals.

No real user vault was opened, modified or migrated.
Risky v4/v5 password changes remain blocked.
The test v6 format remains separate from the product default.
AV3 production writer, journal writer and migration approval gates remain false.
Raw vaults, plaintext payloads, private keys, passwords and recovery codes are excluded from public evidence.

## Qualification boundary

WO25 remains PARTIAL. Phase 1 remains NO-GO.
Use only synthetic files for filesystem and process tests.
Keep actual guest deployment, UAC, independent SID, administrator restore, reboot, and product GUI results separate.
The complete run records are in `evidence/wo25/`.
No main merge or official release is authorized.

## Final automated qualification

Source: `80b00305e96c55064f15734ae932a433a8178f4d`.
Windows CI: [39055483722 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39055483722).
App: 1408 PASS / 0 FAIL / 111 SKIP.
New WO25: 32 PASS, included in the app total.
SecurityLab: 36 PASS, separate from the app.
Restore and Release: 0 warnings / 0 errors.
Actual isolated product GUI, UAC and reboot remain NOT TESTED.
