# WO25 — Authenticated deployment manifest

Baseline: `71d8f3ba85804a61157c0acf553bb37ccf6c07ad`.
Branch: `codex/pccare-wo25-installation-trust-volume-hardening`.

## Input contract

The test manifest format is `pccare-test-deployment-v1`.
It contains deployment version, publisher and the exact relative file list.
Each entry includes length, SHA-256, PE classification and optional publisher override.
Observed file IDs are bound from open handles after manifest authentication.
They are observations, not unsigned replacement trust anchors.
PE files require native Authenticode verification in the strict deployment path.

The exact UTF-8 manifest bytes have a detached ECDSA-P256/SHA-256 P1363 signature.
The caller supplies an independently trusted SHA-256 pin for the public SPKI.
The engine rejects an arbitrary replacement key or modified manifest.
Standard .NET cryptography performs signature verification.
See [ECDsa.VerifyData](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.ecdsa.verifydata?view=net-10.0).
No cryptographic algorithm or production publisher root was changed.

`SignedDeploymentClosure.Bind` checks duplicate JSON properties, path boundaries, file counts, lengths, hashes and actual MZ classification.
The input folder must contain exactly the authenticated files.
Open file and directory leases remain held through validation.
Strict deployment checks protected ACLs before native signature verification.
Content-only unit tests never set `AuthenticodeVerified=true`.

## Limits

The public-key pin needs an external trusted delivery channel.
The same user controlling the signing key and input files is outside this authentication claim.
The manifest authenticates supplied files. It does not authenticate the operating system or shared framework installation.
Actual guest deployment and interrupted guest publication remain NOT TESTED.

## Qualification boundary

WO25 remains PARTIAL. Phase 1 remains NO-GO.
Use only synthetic files for filesystem and process tests.
Keep actual guest deployment, UAC, independent SID, administrator restore, reboot, and product GUI results separate.
The complete run records are in `evidence/wo25/`.
No main merge or official release is authorized.

## Final automated qualification

Source: `80b00305e96c55064f15734ae932a433a8178f4d`.
Windows CI: [39055483722 SUCCESS](https://github.com/scarlel96-design/PC-Care/actions/runs/39055483722).
App: 1408 PASS / 0 FAIL / 111 SKIP.
New WO25: 32 PASS, included in the app total.
SecurityLab: 36 PASS, separate from the app.
Restore and Release: 0 warnings / 0 errors.
Actual isolated product GUI, UAC and reboot remain NOT TESTED.
