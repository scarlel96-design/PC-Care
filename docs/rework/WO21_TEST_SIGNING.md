# WO21 — TEST SIGNING

Status: PARTIAL. Phase 1: NO-GO.

Base HEAD: `1d93600ea5c6214e23051641ad10c06a667f0866`.
Branch: `codex/pccare-wo21-signed-install-recovery-qualification`.
Read `evidence/wo21/VERIFICATION.txt` for final tested-source and publication receipts.

## Reused technology

The implementation retains BuildPublisherTrust, AuthenticodeBoundary, WinVerifyTrust and the WO05 VM build profile.
No dependency version or production publisher root was changed.
AuthenticodeBoundary verifies the signer from retained WinVerifyTrust provider state.
It binds the result to a retained file identity and full SHA-256.
An ECDSA package signature is separate from a PE Authenticode signature.

## Prepared signing tool

`scripts/wo21-test-signing.ps1` defaults to PREPARED output.
Execute mode checks the specified guest UUID, test SID and virtual machine model.
These checks prevent accidental host use. They do not prove a hypervisor checkpoint exists.
The operator must verify that checkpoint on the host first.
The script copies five explicitly named PE files into a new guest test root.
It creates a non-exportable, short-lived guest certificate.
It registers public trust only in the guest CurrentUser store.
It signs each copy and checks status and signer thumbprint.
It removes its own certificate and trust entry in finally.
No private key or certificate password is exported.

The preparation command created no certificate on this host.
No existing host certificate was selected for signing.
No trusted PE transition or timestamp-expiry matrix was executed.
The five-file list and command are recorded in CERTIFICATE_RESULT.json.

## Guest qualification procedure

Verify the guest checkpoint and independent SIDs.
Build the isolated WO05 profile with a disposable guest publisher certificate and package key.
Keep the certificate key inside that guest.
Sign the Bridge, Helper and Engine files after compilation.
Deploy only those test-profile binaries into the guest protected roots.
Keep production-audit rejection of test-profile artifacts enabled.
Run valid, wrong-publisher, unsigned, modified, replaced, expired and chain-failure cases.
Run UAC cancellation before an approved transition.
Verify every installed hash after transition and recovery.
Preserve anonymized results. Restore the verified guest checkpoint.

The prepared PE signing script alone does not implement this complete guest E2E procedure.

## Official references

[SignTool policy and timestamp options](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool).
[Authenticode verification policy](https://learn.microsoft.com/en-us/windows/win32/seccrypto/using-signtool-to-verify-a-file-signature).
[Windows Sandbox configuration](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file).

## Verification source

Final tested source:`930ed7907daaa95cf15040d130c580248e64bf61`.
Windows CI:[38669715643](https://github.com/scarlel96-design/PC-Care/actions/runs/38669715643).
Final publication adds reports and evidence only.
