# WO03 Windows installed-build E2E

STATUS: NOT TESTED for real installed/UAC/reboot E2E. The local Windows runner executed unit tests over temporary directories and a helper unsupported-verb check. It did not change the user's installed PC-Care, trigger UAC, reboot Windows or power-cycle the machine.

A disposable VM test must start from a real 51.x installation and a signed test-channel package trusted only by that VM. Snapshot the VM; record pre-state hashes and user data; verify UAC approval and denial; exercise normal update, interrupted copy, denied access, locked files, restart/recovery and post-install launch/navigation; compare old/new hashes and vault/settings data; then revert the snapshot. A production-channel package needs a separately provisioned official root. The absence of this E2E evidence keeps Phase 1 at NO-GO.
