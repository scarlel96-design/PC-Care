# WO20 — SIGNED TRANSITION

Status: PARTIAL. Phase 1: NO-GO.

Branch: `codex/pccare-wo20-bootstrap-legacy-recovery-e2e`. Base HEAD: `2c140dd51d1eabe35c609d1608e30059d0d6b531`.
Implementation source: `640d2cd0e9d506a4cb66629592ed7d7296e04456`. Source tree: `312830c10ff936c85e7fd81365ff4ae77cf92114`.
The final evidence commit is recorded in GITHUB_RESULT.txt.

## Trust checks

PinBridge holds three files: Bridge.exe, Bridge.dll and UpdateEngine.dll.
Each file requires protected ACLs, actual Authenticode publisher trust, file identity and content hash agreement.
InspectRoute validates the protected installation and protocol metadata.
Protocol metadata alone does not grant execution authority.
Launch verifies the signed package again and revalidates held leases before requesting Windows elevation.
The Bridge validates its fixed execution root and waits for the calling process.
The existing protected snapshot and transactional update engine perform installation work.

## Test signing

The synthetic probe creates an ephemeral ECDSA package key.
That key never becomes a production publisher root or Authenticode certificate.
The private key is not persisted. The fixture password is stored privately with DPAPI CurrentUser.
No certificate was registered in a machine-wide trust store.

## Result

Package-signature and file transaction tests execute on Windows synthetic files.
Authenticode-signed Bridge deployment, UAC approval and first-install transition E2E are NOT TESTED.
A disposable VM, snapshot, independent SID and approved test certificate remain prerequisites.

## Evidence and reproduction

Read `evidence/wo20/VERIFICATION.txt` for commands, inputs, exits and source provenance.
Read the named JSON evidence for literal process outputs and SHA-256 comparisons.
Use only synthetic fixtures. Keep private fixture passwords and DPAPI files outside published evidence.

## Restore procedure

Use `ROLLBACK.sh` only on an independent source copy named RESTORE_COPY.
Use the trusted updater recovery entry point for authenticated schema 2 transactions.
Review schema 1 plans with the signed original-version package and explicit consent.
Preserve files when identity, content, ACL, journal or package checks fail.
Source rollback does not restore installation data or encrypted vault contents.
