# WO23 — Authenticode validation

Baseline: `db8defce0764dd2421ceb78b9e3ce6b851fa9e6b`.
Branch: `codex/pccare-wo23-windows-security-update-hardening`.

## Test-only trust
The CI script creates three short-lived, non-exportable code-signing certificates.
It temporarily trusts two certificates in the disposable runner LocalMachine Root store.
The third certificate has no trusted root.
It removes its own certificate entries and private keys in finally.
It never adds a test certificate to BuildPublisherTrust.Current.

## Matrix
Sign UpdateBridge.exe, UpdateBridge.dll, UpdateEngine.dll, UpdateHelper.exe and UpdateHelper.dll.
Check each unsigned file before signing.
Verify the signed files through AuthenticodeBoundary and its retained WinVerifyTrust provider state.
Check another publisher, modified signed bytes, untrusted chain and copied DLL identity.
Attempt deletion and mutation while the final signed file handle remains pinned.
A valid test signature still fails the production publisher gate.

## Limits
The expected test thumbprint comparison belongs to the qualification tool.
It does not grant production helper execution authority.
Timestamp-service and expired-certificate qualification remain NOT TESTED.
Signed bootstrap deployment, UAC and administrator restore remain OPEN.
[WinVerifyTrust contract](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/nf-wintrust-winverifytrust).
Read `evidence/wo23/AUTHENTICODE_RESULT.json`.
