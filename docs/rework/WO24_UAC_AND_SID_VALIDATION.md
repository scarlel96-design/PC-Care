# WO24 — UAC and SID validation

Baseline: `07acfd724aae16e165afc259ba09dbac537c5eee`.
Branch: `codex/pccare-wo24-isolated-install-recovery-qualification`.
Executable source: `e996082e9656d26564d88d371f6ea992095d2b75`.

WO24Probe access performs bounded create attempts inside an explicit synthetic installation and work root.
The probe records the current administrator role and a hashed SID.
It removes only its newly created probe file.
It never treats the CI administrator as an independent standard user.
Capture the administrator and standard-user reports separately in the approved guest.
Compare distinct SID hashes and the protected-root access results.
UAC is neither disabled nor automated through secure-desktop controls.
UAC approval and denial: NOT TESTED.
Independent SID pair: NOT TESTED.
