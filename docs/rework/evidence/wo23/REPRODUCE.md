# WO23 evidence reproduction

Use executable source `2c1c56b5995f303d57d98098e6eafeefc8e1df11`.
Run commands from the repository root.
Keep generated fixtures outside the repository and installed product paths.
Use an external absolute directory whose name starts with wo21-.
Set RUNNER_TEMP to that directory.
Run scripts/wo21-qualification.ps1 with Mode All and a new output directory.
Run scripts/wo23-process-validation.ps1 after building WO20Probe.
Run the Python runners from the repository root.
Each runner creates independent synthetic files and records exact source inputs.
Run FINAL_MEMORY_PROFILE_RUNNER.py without concurrent builds or tests from this task.
Run LEGACY_PUBLICATION_GAP_RUNNER.py to reproduce the old publication defect.
Run SOURCE_ROLLBACK_RUNNER.py to create and restore independent source copies.
Read SOURCE_ROLLBACK_RESULT.json for the recorded relative paths and literal outputs.
Do not interpret that source restoration as installation or vault recovery.
Run wo23-windows-security.ps1 only through the disposable GitHub-hosted Windows workflow.
The script rejects other environments before creating certificates.
Keep raw fixtures, plaintext, private keys and passwords outside public evidence.
Actual GUI, UAC, reboot and power-loss qualification require separate environments.
