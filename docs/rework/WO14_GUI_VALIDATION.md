# WO-14 gui validation

Branch: `codex/pccare-v5-compatibility-recovery-closure`.

Baseline SHA: `47662986e737f9abc983c040a92aa52472efe557`.

Verified source SHA: `c8c8f604ecc1cec89875c0b57d5ef0bfcbe995ab`.

Source tree: `c95434b61ae67ca361945b9caa74dddea477db8f`.

Environment: Windows NT 10.0.26200.0; .NET SDK 10.0.401; 24 logical processors; synthetic files only.

Status: NOT TESTED for actual GUI; PARTIAL for implementation.

## Implemented UI

The vault page shows a compatibility and recovery warning.
It distinguishes v5 recovery qualification from an approved transition.
The source-sealing checkbox stays disabled and unchecked.
Its Korean text states that source-protection verification is required.
The new notice has a stable AutomationId.
No production transition action is connected to the synthetic API.
The complete requested transition-state UI is not implemented.

## Executed profile guard

The harness returned exit 4:
`{"status":"BLOCKED","guiExecuted":false,"reason":"Dedicated test account required"}`

The host has no qualified independent interactive test account.
No application window was launched by this test.
XAML compilation is not GUI validation.

## Concrete execution procedure

1. Provision an independent standard Windows test account in a disposable VM.
2. Record the original account SID and test account SID privately.
3. Sign in interactively as the test account.
4. Build the Release application and GuiProbe from the verified source.
5. Verify the test profile owner and effective ACL manually.
6. Invoke `wo14-gui-validation.ps1` with both SIDs, binary paths, and `-Launch`.
7. Open the vault page when the harness requests it.
8. Inspect the status and entry accessibility nodes through GuiProbe.
9. Create only synthetic vault content.
10. Test import, export, cancel, and repeated operations.
11. Test narrow windows, high DPI, keyboard focus, screen reader, and reduced motion.
12. Record real results and dispose the VM snapshot.

The harness checks SID separation, profile location, and reparse ancestors.
It records the test-directory ACL but does not validate an expected ACL policy.
`SPD_TEST_VAULT_ROOT` isolates the vault path in Release builds.
Other settings still require the independent user profile.
GuiProbe entry-tree inspection does not qualify the full manual interaction matrix.


## Evidence and restoration

Read `evidence/wo14/VERIFICATION.txt` for commands, inputs, outputs, and exit codes.
Read `evidence/wo14/GITHUB_RESULT.txt` for final remote identity and CI status.
The source rollback restores one independent source copy.
It does not restore a vault or an installation.
Keep the original vault and incomplete transition workspace.
Use a new workspace for each retry.
