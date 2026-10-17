# WO24 — Reboot recovery

Baseline: `07acfd724aae16e165afc259ba09dbac537c5eee`.
Branch: `codex/pccare-wo24-isolated-install-recovery-qualification`.
Executable source: `e996082e9656d26564d88d371f6ea992095d2b75`.

Use wo24-qualification.ps1 Capture before and after an approved guest reboot.
Capture LastBootUpTime from Win32_OperatingSystem.
Hash the observed machine UUID before saving it.
The comparator binds run, machine, source and root identity.
It requires a different nonempty boot identity when RequireReboot is set.
It rejects changed content, failed authentication and pending recovery states.
The input reports are operator-controlled observations, not signed authorization tokens.
A same-boot negative test executes in CI.
Actual reboot and checkpoint restoration: NOT TESTED.

## Guest capture commands

Run both captures under the same source, fixture root and machine.
Capture the baseline after the expected installation state is established.
Use the existing trusted recovery path before the second capture when recovery is required.

```powershell
pwsh -NoProfile -File scripts/wo24-qualification.ps1 -Mode Capture -FixtureRoot $syntheticRoot -OutputFile $beforeJson
# Perform the separately approved guest reboot and required recovery.
pwsh -NoProfile -File scripts/wo24-qualification.ps1 -Mode Capture -FixtureRoot $syntheticRoot -OutputFile $afterJson
pwsh -NoProfile -File scripts/wo24-qualification.ps1 -Mode Compare -Before $beforeJson -After $afterJson -RequireReboot -OutputFile $comparisonJson
```

The fixture must be a WO20-generated synthetic root beneath `RUNNER_TEMP`.
The capture holds file leases through hashing and the final history check.
A changed boot value alone does not prove complete recovery.
The comparison also requires matching authenticated byte observations and completed recovery state.
A fabricated observation can defeat this operator-controlled comparison.
Do not use it as an authorization token or remote attestation.

The same-boot negative trial must return comparison exit code 10.
The PowerShell wrapper records that result and raises a review error.
No actual reboot, VM reset or power interruption occurred here.
