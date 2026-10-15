# WO21 — ENVIRONMENT READINESS

Status: PARTIAL. Phase 1: NO-GO.

Base HEAD: `1d93600ea5c6214e23051641ad10c06a667f0866`.
Branch: `codex/pccare-wo21-signed-install-recovery-qualification`.
Read `evidence/wo21/VERIFICATION.txt` for final tested-source and publication receipts.

## Observed environment

The host reports Windows build 26200 and 24 logical processors.
WindowsSandbox.exe exists. Its optional-feature state remains UNVERIFIED.
Feature queries require Windows elevation. A sandbox executable does not prove a usable guest or checkpoint.
Get-VM, vmrun and VBoxManage were not available through the qualification command inventory.
No selected remote test host, guest SID pair or verified checkpoint was available.
No hypervisor, account, firmware setting or certificate trust store was changed.

## Preparation

Run `scripts/wo21-environment.ps1` for the read-only inventory.
Provide a generalized guest VHD before using `scripts/wo21-prepare-vm.ps1`.
Use Plan mode first. The script does not download Windows or install Hyper-V.
Execute the script only against an authorized disposable guest.
The script copies the VHD before creating a Generation 2 VM.
It creates a checkpoint and queries Hyper-V for that checkpoint.
It does not attach a network switch.
Create separate guest administrator and standard-user accounts through the guest setup procedure.
Record both guest SIDs, AppData, TEMP, installation root and ACLs.
Restore and query the checkpoint after each destructive qualification scenario.

VM_UNAVAILABLE and INTERACTIVE_UAC_UNAVAILABLE remain environmental blockers.
No actual guest installation, reboot or power loss was tested.

## Verification source

Final tested source:`930ed7907daaa95cf15040d130c580248e64bf61`.
Windows CI:[38669715643](https://github.com/scarlel96-design/PC-Care/actions/runs/38669715643).
Final publication adds reports and evidence only.
