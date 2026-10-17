# WO24 — Signed Bridge deployment

Baseline: `07acfd724aae16e165afc259ba09dbac537c5eee`.
Branch: `codex/pccare-wo24-isolated-install-recovery-qualification`.
Executable source: `e996082e9656d26564d88d371f6ea992095d2b75`.

Use scripts/wo24-signed-deployment.ps1 in an approved disposable guest.
The default invocation reports PREPARED without deployment.
Execution checks the actual guest UUID, current SID, administrator role and probe binary hash.
Reuse wo21-test-signing.ps1 for five PE inputs.
Copy required runtime JSON files.
Apply a protected guest directory ACL.
Verify each signed file hash, publisher and native file identity through WO23WindowsProbe.
Remove temporary CurrentUser root trust in finally.
The unsupported-verb Bridge startup expects exit 2.
That startup does not execute bootstrap transition.
A real guest and independently verified checkpoint remain mandatory operator prerequisites.
Guest identity fields are not remote attestation.
Actual guest deployment: PREPARED / NOT TESTED.

## Prepared guest command

Resolve the six values from the approved guest and the source-bound build receipt.
Run this command only inside that disposable guest.

```powershell
pwsh -NoProfile -File scripts/wo24-signed-deployment.ps1 `
  -BinaryDirectory $binaryDirectory -GuestRoot $newGuestRoot `
  -ExpectedGuestUuid $observedGuestUuid -ExpectedSid $observedAdminSid `
  -WindowsProbe $sourceBoundWindowsProbe -ExpectedProbeHash $recordedProbeHash -Execute
```

The guest root must be new and meet the reused WO21 signer boundary.
Preserve each PE hash, file identity and Authenticode result.
Preserve the deployment result and certificate cleanup result.
The script verifies the unsupported-verb startup contract only.
The signed test publisher remains outside the production publisher policy.
A successful startup still requires a separate full transition trial.
Actual transition automation through production trust remains OPEN.

## Cleanup failure contract

The original guest signer suppressed certificate-removal failures.
The revised cleanup attempts both stores and requests deletion of its own private key.
It checks for remaining certificate entries and reports cleanup failure.
The source-copy comparison uses a mock provider that rejects removal.
It proves this error contract, not actual guest certificate-store cleanup.
The retained CI native-signature matrix uses its existing isolated signing harness.

## Input provenance limit

Validate the complete verifier and product build closure before guest execution.
Include dependency DLLs and runtime configuration files in that input receipt.
The script checks the verifier EXE hash, not the complete verifier dependency closure.
That single hash does not authenticate every loaded dependency.
Use protected, source-bound inputs from the approved test build.
Full closure enforcement and actual guest execution remain qualification work.
