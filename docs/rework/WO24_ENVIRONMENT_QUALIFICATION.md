# WO24 — Environment qualification

Baseline: `07acfd724aae16e165afc259ba09dbac537c5eee`.
Branch: `codex/pccare-wo24-isolated-install-recovery-qualification`.
Executable source: `e996082e9656d26564d88d371f6ea992095d2b75`.

Reuse WO23 ENVIRONMENT_RESULT.json and the existing WO21 preparation scripts.
No new disposable guest, verified checkpoint or independent SID pair was supplied.
No repeated hypervisor search was performed.
The new host checkpoint tool calls Get-VM and Get-VMSnapshot before recording an observation.
Its optional restore requires an observed checkpoint and a dedicated PC-Care-WO24- VM name.
A completed hypervisor restore command does not verify guest file bytes.
Guest byte validation remains a separate capture and comparison.
Actual guest environment: VM_UNAVAILABLE.

API syntax references: [Get-VMSnapshot](https://learn.microsoft.com/en-us/powershell/module/hyper-v/get-vmsnapshot?view=windowsserver2025-ps) and [Restore-VMSnapshot](https://learn.microsoft.com/nl-nl/powershell/module/hyper-v/restore-vmsnapshot?view=windowsserver2025-ps).
The documented object parameter sets were checked.
Actual Hyper-V execution remains NOT TESTED.

## Execution sequence

1. Reuse `scripts/wo21-prepare-vm.ps1` with an approved generalized VHD.
2. Use a dedicated VM name with the `PC-Care-WO24-` prefix.
3. Record the VM ID and checkpoint ID from the hypervisor.
4. Run `scripts/wo24-checkpoint.ps1` without `ExecuteRestore`.
5. Record the guest OS, NTFS volumes, machine UUID and account SID pair.
6. Build the exact source and preserve the binary receipt.
7. Run guest signing and deployment under the disposable administrator account.
8. Run protected-root access checks under each separate account.
9. Capture verified guest bytes before an approved reboot.
10. Capture verified guest bytes after the reboot and recovery.
11. Compare both captures with `RequireReboot`.
12. Restore the observed checkpoint after the test.
13. Capture guest bytes again to verify the restored fixture.

Use new output paths for each capture.
A supplied VM ID is input, not proof of a checkpoint.
The host tool queries the hypervisor before recording an observation.
Guest observations are not independently signed attestations.
No host virtualization, security policy or account was changed during this work.

## Runtime and deployment inputs

The product targets `net10.0-windows10.0.26100.0`.
Its project sets `WindowsAppSDKSelfContained=true` and `WindowsPackageType=None`.
Copy the complete source-bound product output for GUI qualification.
Do not treat the five updater PE inputs as a complete product installation.
Record the guest .NET runtime version before execution.
Keep the required Bridge and Helper runtime configuration files with their binaries.
No guest runtime installation was performed here.

The Prepare result describes the currently unprovided guest context.
It does not perform VM inventory or checkpoint discovery.
Use the host checkpoint query and actual guest identity checks after a guest is supplied.
